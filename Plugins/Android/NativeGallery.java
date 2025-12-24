package com.nkstudio.nativegallery;

import android.app.Activity;
import android.content.ContentResolver;
import android.content.ContentValues;
import android.net.Uri;
import android.nfc.Tag;
import android.os.Build;
import android.provider.MediaStore;
import android.util.Log;

import com.unity3d.player.UnityPlayer;

import java.io.File;
import java.io.FileInputStream;
import java.io.OutputStream;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public class NativeGallery {

    private static final String TAG = "NativeGallery";
    private static Activity currentActivity;
    private static ExecutorService executorService;

    private enum MediaType {
        IMAGE, VIDEO, UNKNOWN
    }

    /**
     * 이미지 또는 비디오를 갤러리에 저장
     *
     * @param filePath           임시 경로의 파일 경로
     * @param callbackGameObject 콜백을 받을 유니티 게임오브젝트 이름
     * @param callbackMethodName 콜백 메서드 이름
     * @param requestId          요청 ID (콜백 구분용)
     */
    public static void SaveMediaToGallery(final String filePath, final String albumName, final String fileName,
                                          final String callbackGameObject, final String callbackMethodName,
                                          final int requestId) {
        currentActivity = UnityPlayer.currentActivity;

        if (executorService == null || executorService.isShutdown()) {
            executorService = Executors.newSingleThreadExecutor();
        }

        executorService.execute(new Runnable() {
            @Override
            public void run() {
                try {
                    if (Build.VERSION.SDK_INT < Build.VERSION_CODES.Q) {
                        SendCallback(callbackGameObject, callbackMethodName, requestId, false, "", "API 29+ Required");
                        return;
                    }

                    File file = new File(filePath);
                    if (!file.exists()) {
                        SendCallback(callbackGameObject, callbackMethodName, requestId, false, "", "File not found");
                        return;
                    }

                    String extension = GetExtension(filePath);
                    String fileNameWithExtension = fileName + "." + extension;

                    MediaType mediaType = GetMediaTypeFromExtension(fileNameWithExtension); // filePath 기준 판단

                    // 전달받은 albumName과 fileName을 사용하도록 수정
                    String savedPath = SaveToMediaStore(file, mediaType, albumName, fileNameWithExtension);

                    if (savedPath != null && !savedPath.isEmpty()) {
                        SendCallback(callbackGameObject, callbackMethodName, requestId, true, savedPath, "Success");
                    } else {
                        SendCallback(callbackGameObject, callbackMethodName, requestId, false, "", "Save Failed");
                    }
                } catch (Exception e) {
                    SendCallback(callbackGameObject, callbackMethodName, requestId, false, "", e.getMessage());
                }
            }
        });
    }

    /**
     * MediaStore를 사용하여 미디어를 갤러리에 저장
     *
     * @param file      저장할 파일
     * @param mediaType 미디어 타입 (IMAGE 또는 VIDEO)
     * @return 저장된 미디어의 URI 문자열, 실패시 null
     */
    private static String SaveToMediaStore(File file, MediaType mediaType, String albumName, String fileName) {
        OutputStream outputStream = null;
        FileInputStream inputStream = null;

        try {
            ContentResolver contentResolver = currentActivity.getContentResolver();

            // ContentValues 설정
            ContentValues values = new ContentValues();
            values.put(MediaStore.MediaColumns.DISPLAY_NAME, fileName); // fileName 적용
            values.put(MediaStore.MediaColumns.MIME_TYPE, GetMimeType(fileName));
            values.put(MediaStore.MediaColumns.IS_PENDING, 1);

            Uri contentUri;

            if (mediaType == MediaType.IMAGE) {
                values.put(MediaStore.Images.Media.DATE_TAKEN, System.currentTimeMillis()); // 밀리초 단위 사용
                values.put(MediaStore.Images.Media.RELATIVE_PATH, "DCIM/" + albumName); // albumName 적용
                contentUri = MediaStore.Images.Media.EXTERNAL_CONTENT_URI;
            } else if (mediaType == MediaType.VIDEO) {
                values.put(MediaStore.Video.Media.DATE_TAKEN, System.currentTimeMillis()); // 밀리초 단위 사용
                values.put(MediaStore.Video.Media.RELATIVE_PATH, "DCIM/" + albumName);
                contentUri = MediaStore.Video.Media.EXTERNAL_CONTENT_URI;
            } else {
                Log.e(TAG, "Unsupported media type: " + mediaType);
                return null;
            }

            Log.d(TAG, "Inserting media to MediaStore");

            // MediaStore에 미디어 삽입
            Uri mediaUri = contentResolver.insert(contentUri, values);

            if (mediaUri == null) {
                Log.e(TAG, "Failed to create MediaStore entry");
                return null;
            }

            Log.d(TAG, "MediaStore entry created: " + mediaUri.toString());

            // 파일 데이터 복사
            outputStream = contentResolver.openOutputStream(mediaUri);
            inputStream = new FileInputStream(file);

            if (outputStream == null) {
                Log.e(TAG, "Failed to open output stream");
                return null;
            }

            byte[] buffer = new byte[8192];
            int bytesRead;
            long totalBytes = 0;

            while ((bytesRead = inputStream.read(buffer)) != -1) {
                outputStream.write(buffer, 0, bytesRead);
                totalBytes += bytesRead;
            }
            outputStream.flush();

            Log.d(TAG, "Copied " + totalBytes + " bytes");

            // IS_PENDING 플래그 제거하여 갤러리에서 보이도록 설정
            values.clear();
            values.put(MediaStore.MediaColumns.IS_PENDING, 0);
            contentResolver.update(mediaUri, values, null, null);

            Log.d(TAG, "Media finalized in gallery");

            // URI 문자열 반환
            return mediaUri.toString();

        } catch (Exception e) {
            Log.e(TAG, "Error in SaveToMediaStore", e);
            return null;
        } finally {
            // 리소스 정리
            try {
                if (outputStream != null) {
                    outputStream.close();
                }
                if (inputStream != null) {
                    inputStream.close();
                }
            } catch (Exception e) {
                Log.e(TAG, "Error closing streams", e);
            }
        }
    }

    /**
     * 파일 확장자로부터 미디어 타입 판단
     *
     * @param fileName 파일 이름
     * @return 미디어 타입 (IMAGE, VIDEO, UNKNOWN)
     */
    private static MediaType GetMediaTypeFromExtension(String fileName) {
        String extension = GetExtension(fileName);

        // 이미지 확장자
        switch (extension) {
            case "png":
            case "jpg":
            case "jpeg":
            case "gif":
            case "webp":
            case "bmp":
            case "heic":
            case "heif":
                return MediaType.IMAGE;
        }

        // 비디오 확장자
        switch (extension) {
            case "mp4":
            case "mov":
            case "avi":
            case "mkv":
            case "webm":
            case "3gp":
            case "m4v":
            case "flv":
                return MediaType.VIDEO;
        }

        return MediaType.UNKNOWN;
    }

    /**
     * 파일 확장자로부터 MIME 타입 추론
     *
     * @param fileName 파일 이름
     * @return MIME 타입
     */
    private static String GetMimeType(String fileName) {
        String extension = GetExtension(fileName);

        // 이미지 MIME 타입
        switch (extension) {
            case "png":
                return "image/png";
            case "jpg":
            case "jpeg":
                return "image/jpeg";
            case "gif":
                return "image/gif";
            case "webp":
                return "image/webp";
            case "bmp":
                return "image/bmp";
            case "heic":
                return "image/heic";
            case "heif":
                return "image/heif";
        }

        // 비디오 MIME 타입
        switch (extension) {
            case "mp4":
                return "video/mp4";
            case "mov":
                return "video/quicktime";
            case "avi":
                return "video/x-msvideo";
            case "mkv":
                return "video/x-matroska";
            case "webm":
                return "video/webm";
            case "3gp":
                return "video/3gpp";
            case "m4v":
                return "video/x-m4v";
            case "flv":
                return "video/x-flv";
        }

        // 기본값
        MediaType type = GetMediaTypeFromExtension(fileName);
        if (type == MediaType.IMAGE) {
            return "image/*";
        } else if (type == MediaType.VIDEO) {
            return "video/*";
        }

        return "application/octet-stream";
    }

    /**
     * 파일명에서 확장자 추출
     *
     * @param fileName 파일 이름
     * @return 소문자 확장자
     */
    private static String GetExtension(String fileName) {
        int lastDot = fileName.lastIndexOf('.');
        if (lastDot > 0 && lastDot < fileName.length() - 1) {
            return fileName.substring(lastDot + 1).toLowerCase();
        }
        return "";
    }

    /**
     * 결과를 유니티로 전송
     *
     * @param gameObject 게임오브젝트 이름
     * @param methodName 메서드 이름
     * @param requestId  요청 ID
     * @param success    성공 여부
     * @param path       저장된 경로
     * @param message    메시지
     */
    private static void SendCallback(final String gameObject, final String methodName,
                                     final int requestId, final boolean success,
                                     final String path, final String message) {
        if (currentActivity == null) {
            Log.e(TAG, "currentActivity is null, cannot send callback");
            return;
        }

        currentActivity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                try {
                    // Format: requestId|success|path|message
                    String result = requestId + "|" + success + "|" + path + "|" + message;
                    Log.d(TAG, "Sending callback: " + result);
                    UnityPlayer.UnitySendMessage(gameObject, methodName, result);
                } catch (Exception e) {
                    Log.e(TAG, "Error sending callback to Unity", e);
                }
            }
        });
    }
}