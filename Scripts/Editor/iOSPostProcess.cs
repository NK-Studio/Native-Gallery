#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace NKStudio
{
    public class iOSPostProcess
    {
        [PostProcessBuild(1)]
        public static void OnPostprocessBuild(BuildTarget target, string buildPath)
        {
            if (!NativeGallerySettings.Instance.AutomatedSetup)
            {
                Debug.Log("[NativeGallery] Automated setup is disabled. Skipping post-process build.");
                return;
            }

            if (target == BuildTarget.iOS)
            {
                Debug.Log("[NativeGallery] Starting iOS post-process build...");
                
                string pbxProjectPath = PBXProject.GetPBXProjectPath(buildPath);
                string plistPath = Path.Combine(buildPath, "Info.plist");
                
                // Info.plist 설정
                ConfigurePlist(plistPath);
                
                Debug.Log("[NativeGallery] iOS post-process build completed successfully.");
            }
        }
        
        private static void ConfigurePlist(string plistPath)
        {
            PlistDocument plist = new PlistDocument();
            plist.ReadFromString(File.ReadAllText(plistPath));

            PlistElementDict rootDict = plist.root;
            
            // iOS 14+ 필수 권한 (읽기 및 쓰기)
            rootDict.SetString("NSPhotoLibraryUsageDescription", 
                NativeGallerySettings.Instance.PhotoLibraryUsageDescription);
            
            // iOS 14+ 추가 전용 권한 (쓰기 전용)
            rootDict.SetString("NSPhotoLibraryAddUsageDescription",
                NativeGallerySettings.Instance.PhotoLibraryAdditionsUsageDescription);
            
            // iOS 14+ 제한된 사진 라이브러리 자동 알림 방지
            if (NativeGallerySettings.Instance.DontAskLimitedPhotosPermissionAutomaticallyOnIos14)
            {
                rootDict.SetBoolean("PHPhotoLibraryPreventAutomaticLimitedAccessAlert", true);
            }
            
            // iOS 최소 버전 설정 확인 (iOS 15+ 권장)
            if (!rootDict.values.ContainsKey("MinimumOSVersion"))
            {
                rootDict.SetString("MinimumOSVersion", "15.0");
                Debug.Log("[NativeGallery] Set MinimumOSVersion to 15.0");
            }
            
            // UIFileSharingEnabled 설정 (선택사항, 파일 앱 통합)
            // rootDict.SetBoolean("UIFileSharingEnabled", true);
            // rootDict.SetBoolean("LSSupportsOpeningDocumentsInPlace", true);

            File.WriteAllText(plistPath, plist.WriteToString());
            
            Debug.Log("[NativeGallery] Info.plist configured with privacy descriptions");
        }
    }
}
#endif