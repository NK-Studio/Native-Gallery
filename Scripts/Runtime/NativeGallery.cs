using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using System.Collections;
#endif

namespace NKStudio
{
    public static class NativeGallery
    {
        private static int nextRequestId = 0;
        private static Dictionary<int, Action<bool, string>> callbacks = new();
        private static GameObject callbackHandler;

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaClass nativeGalleryClass;
#endif

#if UNITY_IOS && !UNITY_EDITOR
        // iOS Native 함수 import
        [DllImport("__Internal")]
        private static extern void SaveMediaToGallery_iOS(
            string filePath,
            string albumName,
            string fileName,
            int requestId,
            SaveMediaCallbackDelegate callback
        );

        // iOS 콜백 델리게이트
        private delegate void SaveMediaCallbackDelegate(
            int requestId,
            bool success,
            IntPtr pathPtr,
            IntPtr messagePtr
        );

        [AOT.MonoPInvokeCallback(typeof(SaveMediaCallbackDelegate))]
        private static void OnIOSCallback(int requestId, bool success, IntPtr pathPtr, IntPtr messagePtr)
        {
            string path = pathPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(pathPtr) : "";
            string message = messagePtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(messagePtr) : "";

            if (callbacks.TryGetValue(requestId, out Action<bool, string> callback))
            {
                callbacks.Remove(requestId);
                
                // 메인 스레드에서 콜백 실행
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    callback?.Invoke(success, success ? path : message);
                });
            }
        }
#endif

        /// <summary>
        /// 이미지 또는 비디오를 갤러리에 저장합니다.
        /// Android 29+ / iOS 15+ 권장
        /// </summary>
        /// <param name="imagePath">저장할 원본 파일의 절대 경로</param>
        /// <param name="albumName">갤러리에 생성될 앨범(폴더) 이름</param>
        /// <param name="fileName">저장할 파일 이름 (확장자 제외)</param>
        /// <param name="callback">완료 콜백 (success: 성공 여부, path: 저장된 경로 또는 에러 메시지)</param>
        public static void SaveToGallery(string imagePath, string albumName, string fileName,
            Action<bool, string> callback = null)
        {
            if (string.IsNullOrEmpty(imagePath) || !System.IO.File.Exists(imagePath))
            {
                callback?.Invoke(false, "Invalid file path");
                return;
            }

            InitializeCallbackHandler();

#if UNITY_ANDROID && !UNITY_EDITOR
            NativeGalleryCallbackReceiver receiver = callbackHandler.GetComponent<NativeGalleryCallbackReceiver>();
            receiver.StartCoroutine(SaveMediaAsync(imagePath, albumName, fileName, callback));
#elif UNITY_IOS && !UNITY_EDITOR
            SaveMediaIOS(imagePath, albumName, fileName, callback);
#else
            callback?.Invoke(false, "Supported on Android and iOS only");
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static IEnumerator SaveMediaAsync(string imagePath, string albumName, string fileName,
            Action<bool, string> callback)
        {
            yield return null;

            try
            {
                if (nativeGalleryClass == null)
                {
                    nativeGalleryClass = new AndroidJavaClass("com.nkstudio.nativegallery.NativeGallery");
                }

                int requestId = nextRequestId++;
                callbacks[requestId] = callback;

                nativeGalleryClass.CallStatic("SaveMediaToGallery",
                    imagePath,
                    albumName,
                    fileName,
                    callbackHandler.name,
                    "OnGallerySaveCallback",
                    requestId);
            }
            catch (Exception e)
            {
                callback?.Invoke(false, e.Message);
            }
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        private static void SaveMediaIOS(string imagePath, string albumName, string fileName,
            Action<bool, string> callback)
        {
            try
            {
                int requestId = nextRequestId++;
                callbacks[requestId] = callback;

                SaveMediaToGallery_iOS(imagePath, albumName, fileName, requestId, OnIOSCallback);
            }
            catch (Exception e)
            {
                callback?.Invoke(false, e.Message);
            }
        }
#endif

        private static void InitializeCallbackHandler()
        {
            if (callbackHandler == null)
            {
                callbackHandler = new GameObject("NativeGalleryCallbackHandler");
                callbackHandler.AddComponent<NativeGalleryCallbackReceiver>();
#if UNITY_IOS && !UNITY_EDITOR
                callbackHandler.AddComponent<UnityMainThreadDispatcher>();
#endif
                UnityEngine.Object.DontDestroyOnLoad(callbackHandler);
            }
        }

        /// <summary>
        /// Android Java의 UnitySendMessage를 통해 호출됨
        /// </summary>
        private static void ReceiveCallback(string result)
        {
            try
            {
                string[] parts = result.Split('|');
                if (parts.Length < 4) return;

                int requestId = int.Parse(parts[0]);
                bool success = bool.Parse(parts[1].ToLower());
                string path = parts[2];
                string message = parts[3];

                if (callbacks.TryGetValue(requestId, out Action<bool, string> callback))
                {
                    callbacks.Remove(requestId);
                    callback?.Invoke(success, success ? path : message);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"ReceiveCallback Error: {e.Message}");
            }
        }

        private class NativeGalleryCallbackReceiver : MonoBehaviour
        {
            private void OnGallerySaveCallback(string result)
            {
                ReceiveCallback(result);
            }
        }
    }

    // iOS 메인 스레드 디스패처 (콜백을 메인 스레드에서 실행하기 위함)
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher _instance;
        private readonly Queue<Action> _executionQueue = new Queue<Action>();

        public static UnityMainThreadDispatcher Instance()
        {
            if (_instance == null)
            {
                throw new Exception("UnityMainThreadDispatcher not initialized");
            }
            return _instance;
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Update()
        {
            lock (_executionQueue)
            {
                while (_executionQueue.Count > 0)
                {
                    _executionQueue.Dequeue().Invoke();
                }
            }
        }

        public void Enqueue(Action action)
        {
            lock (_executionQueue)
            {
                _executionQueue.Enqueue(action);
            }
        }
    }
}