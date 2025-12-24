import Foundation
import Photos
import UIKit

// MARK: - C 스타일 콜백 타입 정의
typealias SaveMediaCallback = @convention(c) (Int32, Bool, UnsafePointer<CChar>?, UnsafePointer<CChar>?) -> Void

// MARK: - Unity Bridge Functions
@_cdecl("SaveMediaToGallery_iOS")
func SaveMediaToGallery_iOS(
    filePath: UnsafePointer<CChar>,
    albumName: UnsafePointer<CChar>,
    fileName: UnsafePointer<CChar>,
    requestId: Int32,
    callback: @escaping SaveMediaCallback
) {
    let filePathStr = String(cString: filePath)
    let albumNameStr = String(cString: albumName)
    let fileNameStr = String(cString: fileName)
    
    // 백그라운드 스레드에서 처리
    DispatchQueue.global(qos: .userInitiated).async {
        NativeGalleryManager.shared.saveMediaToGallery(
            filePath: filePathStr,
            albumName: albumNameStr,
            fileName: fileNameStr,
            requestId: requestId,
            callback: callback
        )
    }
}

// MARK: - Native Gallery Manager
class NativeGalleryManager {
    static let shared = NativeGalleryManager()
    
    private init() {}
    
    func saveMediaToGallery(
        filePath: String,
        albumName: String,
        fileName: String,
        requestId: Int32,
        callback: @escaping SaveMediaCallback
    ) {
        // iOS 15+ 타겟팅
        guard #available(iOS 15, *) else {
            sendCallback(requestId: requestId, success: false, path: nil, message: "iOS 15+ required", callback: callback)
            return
        }
        
        // 파일 존재 확인
        let fileURL = URL(fileURLWithPath: filePath)
        guard FileManager.default.fileExists(atPath: filePath) else {
            sendCallback(requestId: requestId, success: false, path: nil, message: "File not found", callback: callback)
            return
        }
        
        // 파일 확장자 확인
        let fileExtension = fileURL.pathExtension.lowercased()
        let mediaType = getMediaType(from: fileExtension)
        
        guard mediaType != .unknown else {
            sendCallback(requestId: requestId, success: false, path: nil, message: "Unsupported file type", callback: callback)
            return
        }
        
        // 사진 라이브러리 권한 확인
        let status = PHPhotoLibrary.authorizationStatus(for: .addOnly)
        
        if status == .authorized || status == .limited {
            performSave(fileURL: fileURL, albumName: albumName, fileName: fileName, mediaType: mediaType, requestId: requestId, callback: callback)
        } else if status == .notDetermined {
            // 권한 요청
            PHPhotoLibrary.requestAuthorization(for: .addOnly) { newStatus in
                if newStatus == .authorized || newStatus == .limited {
                    self.performSave(fileURL: fileURL, albumName: albumName, fileName: fileName, mediaType: mediaType, requestId: requestId, callback: callback)
                } else {
                    self.sendCallback(requestId: requestId, success: false, path: nil, message: "Photo library access denied", callback: callback)
                }
            }
        } else {
            sendCallback(requestId: requestId, success: false, path: nil, message: "Photo library access denied", callback: callback)
        }
    }
    
    private func performSave(
        fileURL: URL,
        albumName: String,
        fileName: String,
        mediaType: MediaType,
        requestId: Int32,
        callback: @escaping SaveMediaCallback
    ) {
        PHPhotoLibrary.shared().performChanges({
            // 현재 날짜로 생성 요청 생성
            let creationRequest: PHAssetChangeRequest
            
            switch mediaType {
            case .image:
                guard let image = UIImage(contentsOfFile: fileURL.path) else {
                    return
                }
                
                // EXIF 데이터 제거하고 현재 시간으로 저장
                creationRequest = PHAssetChangeRequest.creationRequestForAsset(from: image)
                
            case .video:
                creationRequest = PHAssetChangeRequest.creationRequestForAssetFromVideo(atFileURL: fileURL)!
                
            case .unknown:
                return
            }
            
            // 현재 날짜로 생성일 설정
            creationRequest.creationDate = Date()
            
            // 앨범에 추가 (선택사항)
            if !albumName.isEmpty {
                self.addToAlbum(assetRequest: creationRequest, albumName: albumName)
            }
            
        }, completionHandler: { success, error in
            if success {
                // 성공 시 로컬 식별자 반환
                self.sendCallback(requestId: requestId, success: true, path: "saved", message: "Success", callback: callback)
            } else {
                let errorMessage = error?.localizedDescription ?? "Save failed"
                self.sendCallback(requestId: requestId, success: false, path: nil, message: errorMessage, callback: callback)
            }
        })
    }
    
    private func addToAlbum(assetRequest: PHAssetChangeRequest, albumName: String) {
        // 앨범 찾기 또는 생성
        let fetchOptions = PHFetchOptions()
        fetchOptions.predicate = NSPredicate(format: "title = %@", albumName)
        let collection = PHAssetCollection.fetchAssetCollections(with: .album, subtype: .any, options: fetchOptions)
        
        if let album = collection.firstObject {
            // 기존 앨범에 추가
            if let albumChangeRequest = PHAssetCollectionChangeRequest(for: album),
               let placeholder = assetRequest.placeholderForCreatedAsset {
                albumChangeRequest.addAssets([placeholder] as NSArray)
            }
        } else {
            // 새 앨범 생성
            if let placeholder = assetRequest.placeholderForCreatedAsset {
                PHAssetCollectionChangeRequest.creationRequestForAssetCollection(withTitle: albumName)
                    .addAssets([placeholder] as NSArray)
            }
        }
    }
    
    private func sendCallback(
        requestId: Int32,
        success: Bool,
        path: String?,
        message: String,
        callback: SaveMediaCallback
    ) {
        let pathCString = path?.cString(using: .utf8)
        let messageCString = message.cString(using: .utf8)
        
        // 메인 스레드에서 콜백 호출
        DispatchQueue.main.async {
            pathCString?.withUnsafeBufferPointer { pathBuffer in
                messageCString?.withUnsafeBufferPointer { messageBuffer in
                    callback(requestId, success, pathBuffer.baseAddress, messageBuffer.baseAddress)
                }
            }
        }
    }
    
    // MARK: - Helper Functions
    private func getMediaType(from fileExtension: String) -> MediaType {
        switch fileExtension {
        case "png", "jpg", "jpeg", "gif", "heic", "heif", "webp", "bmp":
            return .image
        case "mp4", "mov", "m4v", "avi", "mkv", "webm":
            return .video
        default:
            return .unknown
        }
    }
}

// MARK: - Media Type Enum
enum MediaType {
    case image
    case video
    case unknown
}
