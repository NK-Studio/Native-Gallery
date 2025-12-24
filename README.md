# Native Gallery 사용 가이드

## 개요

Native Gallery는 Unity에서 Android 및 iOS 기기의 갤러리에 이미지와 비디오를 저장할 수 있는 네이티브 플러그인입니다.

### 지원 플랫폼
- **Android**: API Level 29 (Android 10) 이상
- **iOS**: iOS 15.0 이상

---

## 목차
- [설치](#설치)
- [iOS 설정](#ios-설정)
- [사용 방법](#사용-방법)
  - [기본 API](#기본-api)
  - [예제 1: 웹에서 이미지 저장](#예제-1-웹에서-다운받은-이미지-저장하기)
  - [예제 2: 웹에서 비디오 저장](#예제-2-웹에서-다운받은-비디오-저장하기)
  - [예제 3: 스크린샷 저장](#예제-3-스크린샷-저장하기)
- [주요 기능](#주요-기능)
- [권한 처리](#권한-처리)
- [문제 해결](#문제-해결)

---

## 설치

### 1. 유니티 패키지 임포트
[다운로드](../../releases/latest)를 클릭하여 `NativeGallery.unitypackage`를 유니티에 설치합니다.

### 2. iOS 설정
![Native Gallery Preview](https://github.com/NK-Studio/Native-Gallery/blob/main/%20preview.jpg)
**Project Settings → NKStudio → Native Gallery**에서 권한 설정:

1. Unity 에디터에서 `Edit → Project Settings` 메뉴 열기
2. 좌측 메뉴에서 **`NKStudio → Native Gallery`** 선택
3. 다음 항목 설정:
   - ✅ **자동 설정**: 체크 (권장)
   - 📝 **사진 라이브러리 사용 설명**: 사용자에게 보여질 권한 요청 메시지 입력
   - 📝 **사진 라이브러리 추가 기능 사용 설명**: 저장 전용 권한 요청 메시지 입력
   - ✅ **제한된 사진 허가를 자동으로 요청하지 마세요**: 체크 (권장)

**예시 권한 메시지:**
- "게임 내 스크린샷을 갤러리에 저장하기 위해 사진 라이브러리 접근이 필요합니다."
- "캡처한 이미지를 저장하기 위해 권한이 필요합니다."

> ⚠️ **중요**: iOS에서는 Info.plist에 권한 설명이 없으면 앱이 크래시됩니다. 자동 설정을 활성화하면 빌드 시 자동으로 추가됩니다.

---

## 사용 방법

### 기본 API

```csharp
NativeGallery.SaveToGallery(
    imagePath: string,              // 저장할 파일의 절대 경로
    albumName: string,              // 앨범(폴더) 이름
    fileName: string,               // 파일 이름 (확장자 제외)
    callback: Action<bool, string>  // 완료 콜백
);
```

### 예제 1: 웹에서 다운받은 이미지 저장하기

```csharp
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using NKStudio;

public class ImageDownloader : MonoBehaviour
{
    [SerializeField] private string imageUrl = "https://example.com/image.jpg";
    
    public void DownloadAndSaveImage()
    {
        StartCoroutine(DownloadImageCoroutine());
    }
    
    private IEnumerator DownloadImageCoroutine()
    {
        Debug.Log("이미지 다운로드 시작...");
        
        // 웹에서 이미지 다운로드
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl))
        {
            yield return request.SendWebRequest();
            
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"이미지 다운로드 실패: {request.error}");
                yield break;
            }
            
            // 다운로드한 텍스처 가져오기
            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            
            // 임시 파일로 저장
            string fileName = $"downloaded_image_{System.DateTime.Now.Ticks}";
            string tempPath = System.IO.Path.Combine(Application.temporaryCachePath, fileName + ".jpg");
            
            // JPG로 인코딩하여 저장
            byte[] imageBytes = texture.EncodeToJPG(90);
            System.IO.File.WriteAllBytes(tempPath, imageBytes);
            
            Debug.Log($"임시 파일 저장 완료: {tempPath}");
            
            // 갤러리에 저장
            NativeGallery.SaveToGallery(
                imagePath: tempPath,
                albumName: "MyApp",
                fileName: fileName,
                callback: (success, message) =>
                {
                    if (success)
                    {
                        Debug.Log($"✅ 갤러리 저장 성공: {message}");
                        
                        // 임시 파일 삭제 (선택사항)
                        if (System.IO.File.Exists(tempPath))
                        {
                            System.IO.File.Delete(tempPath);
                            Debug.Log("임시 파일 삭제 완료");
                        }
                    }
                    else
                    {
                        Debug.LogError($"❌ 갤러리 저장 실패: {message}");
                    }
                }
            );
        }
    }
}
```

### 예제 2: 웹에서 다운받은 비디오 저장하기

```csharp
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using NKStudio;

public class VideoDownloader : MonoBehaviour
{
    [SerializeField] private string videoUrl = "https://example.com/video.mp4";
    
    public void DownloadAndSaveVideo()
    {
        StartCoroutine(DownloadVideoCoroutine());
    }
    
    private IEnumerator DownloadVideoCoroutine()
    {
        Debug.Log("비디오 다운로드 시작...");
        
        // 웹에서 비디오 다운로드
        using (UnityWebRequest request = UnityWebRequest.Get(videoUrl))
        {
            // 다운로드 진행률 표시 (선택사항)
            var operation = request.SendWebRequest();
            
            while (!operation.isDone)
            {
                Debug.Log($"다운로드 진행률: {operation.progress * 100:F1}%");
                yield return null;
            }
            
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"비디오 다운로드 실패: {request.error}");
                yield break;
            }
            
            // 임시 파일로 저장
            string fileName = $"downloaded_video_{System.DateTime.Now.Ticks}";
            string tempPath = System.IO.Path.Combine(Application.temporaryCachePath, fileName + ".mp4");
            
            // 다운로드한 데이터를 파일로 저장
            System.IO.File.WriteAllBytes(tempPath, request.downloadHandler.data);
            
            Debug.Log($"임시 파일 저장 완료: {tempPath} ({request.downloadHandler.data.Length} bytes)");
            
            // 갤러리에 저장
            NativeGallery.SaveToGallery(
                imagePath: tempPath,
                albumName: "MyApp",
                fileName: fileName,
                callback: (success, message) =>
                {
                    if (success)
                    {
                        Debug.Log($"✅ 갤러리 저장 성공: {message}");
                        
                        // 임시 파일 삭제 (선택사항)
                        if (System.IO.File.Exists(tempPath))
                        {
                            System.IO.File.Delete(tempPath);
                            Debug.Log("임시 파일 삭제 완료");
                        }
                    }
                    else
                    {
                        Debug.LogError($"❌ 갤러리 저장 실패: {message}");
                    }
                }
            );
        }
    }
}
```

### 예제 3: 스크린샷 저장하기

```csharp
using UnityEngine;
using System.Collections;
using NKStudio;

public class ScreenshotCapture : MonoBehaviour
{
    public void CaptureAndSave()
    {
        StartCoroutine(CaptureScreenshotCoroutine());
    }
    
    private IEnumerator CaptureScreenshotCoroutine()
    {
        // 프레임 렌더링 대기
        yield return new WaitForEndOfFrame();
        
        // 스크린샷 캡처
        Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
        
        // 임시 파일로 저장
        string fileName = $"screenshot_{System.DateTime.Now:yyyyMMdd_HHmmss}";
        string tempPath = System.IO.Path.Combine(Application.temporaryCachePath, fileName + ".png");
        
        byte[] imageBytes = screenshot.EncodeToPNG();
        System.IO.File.WriteAllBytes(tempPath, imageBytes);
        
        // 메모리 해제
        Destroy(screenshot);
        
        // 갤러리에 저장
        NativeGallery.SaveToGallery(
            imagePath: tempPath,
            albumName: "MyGame Screenshots",
            fileName: fileName,
            callback: (success, message) =>
            {
                if (success)
                {
                    Debug.Log("✅ 스크린샷 저장 완료!");
                }
                else
                {
                    Debug.LogError($"❌ 스크린샷 저장 실패: {message}");
                }
                
                // 임시 파일 삭제
                if (System.IO.File.Exists(tempPath))
                {
                    System.IO.File.Delete(tempPath);
                }
            }
        );
    }
}
```

---

## 주요 기능

### 📁 커스텀 앨범 지원
- **Android**: `DCIM/앨범이름` 경로에 저장
- **iOS**: 지정한 앨범 이름으로 자동 생성 또는 기존 앨범에 추가

### 🎬 지원 파일 형식
- **이미지**: PNG, JPG, JPEG, GIF, WEBP, BMP, HEIC, HEIF
- **비디오**: MP4, MOV, M4V, AVI, MKV, WEBM, 3GP, FLV

---

## 권한 처리

### Android
- API 29+ (Android 10 이상)에서는 [Scoped Storage](https://developer.android.com/training/data-storage#scoped-storage) 사용
- 별도의 런타임 권한 요청 불필요
- `WRITE_EXTERNAL_STORAGE` 권한 불필요

### iOS
- 첫 실행 시 자동으로 권한 요청 팝업 표시
- "추가 전용" 권한 사용 ([최소 권한 원칙](https://developer.apple.com/documentation/photokit/phphotolibrary/3616113-requestauthorization))
- [Project Settings](#ios-설정)에서 설정한 메시지가 팝업에 표시됨

---

## 문제 해결

### 파일이 갤러리에 나타나지 않을 때
1. 파일 경로가 올바른지 확인
2. 파일이 실제로 존재하는지 확인 (`File.Exists()`)
3. 콜백에서 `success`가 `true`인지 확인
4. **iOS**: 사진 앱을 강제 종료 후 재실행
5. **Android**: 갤러리 앱을 새로고침하거나 재시작

### Android에서 "API 29+ Required" 에러
- 빌드 설정에서 Minimum API Level을 29 이상으로 설정
- `Edit → Project Settings → Player → Android → Minimum API Level`

### iOS에서 권한 거부 시
1. iOS 설정 앱 → 개인정보 보호 → 사진
2. 해당 앱 찾기 → 권한 변경
3. 앱 재시작

### 빌드 오류 발생 시
- **iOS**: Xcode에서 Swift 5.0 이상 설정 확인
- **iOS**: Photos.framework 링크 확인

---

## 라이선스

이 플러그인은 자유롭게 사용할 수 있습니다.

## 지원

문제가 발생하면 다음을 확인하세요:
1. Unity 버전 (2019.3 이상 권장)
2. 플랫폼 최소 버전 (Android 10 / iOS 15)
3. [Project Settings](#ios-설정)의 권한 설정
4. 콘솔 로그의 에러 메시지

---

**제작**: NKStudio  
**버전**: 1.0.0  
**최종 수정**: 2024-12-24
