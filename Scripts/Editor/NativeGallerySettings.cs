using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio
{
    public class NativeGallerySettings : ScriptableObject
    {
        private const string SettingsPath = "Assets/Editor/NativeGallerySettings.asset";

        [SerializeField] private bool automatedSetup = true;
        [SerializeField] private string photoLibraryUsageDescription = "사진을 갤러리에 저장하기 위해 권한이 필요합니다";
        [SerializeField] private string photoLibraryAdditionsUsageDescription = "사진을 갤러리에 저장하기 위해 권한이 필요합니다";
        [SerializeField] private bool dontAskLimitedPhotosPermissionAutomaticallyOnIos14 = true;

        public bool AutomatedSetup
        {
            get => automatedSetup;
            set => automatedSetup = value;
        }

        public string PhotoLibraryUsageDescription
        {
            get => photoLibraryUsageDescription;
            set => photoLibraryUsageDescription = value;
        }

        public string PhotoLibraryAdditionsUsageDescription
        {
            get => photoLibraryAdditionsUsageDescription;
            set => photoLibraryAdditionsUsageDescription = value;
        }

        public bool DontAskLimitedPhotosPermissionAutomaticallyOnIos14
        {
            get => dontAskLimitedPhotosPermissionAutomaticallyOnIos14;
            set => dontAskLimitedPhotosPermissionAutomaticallyOnIos14 = value;
        }

        private static NativeGallerySettings _instance;

        public static NativeGallerySettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = AssetDatabase.LoadAssetAtPath<NativeGallerySettings>(SettingsPath);

                    if (_instance == null)
                    {
                        _instance = CreateInstance<NativeGallerySettings>();

                        // 폴더가 없으면 생성
                        string directory = System.IO.Path.GetDirectoryName(SettingsPath);
                        if (!System.IO.Directory.Exists(directory))
                        {
                            System.IO.Directory.CreateDirectory(directory);
                        }

                        AssetDatabase.CreateAsset(_instance, SettingsPath);
                        AssetDatabase.SaveAssets();
                    }
                }

                return _instance;
            }
        }

        public void Save()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        [SettingsProvider]
        public static SettingsProvider CreatePreferencesGUI()
        {
            var provider = new SettingsProvider("Project/NKStudio/Native Gallery", SettingsScope.Project)
            {
                label = "Native Gallery",
                keywords = new System.Collections.Generic.HashSet<string>() { "Native", "Gallery", "Android", "iOS" },

                activateHandler = (searchContext, rootElement) =>
                {
                    // 스타일 추가
                    var ussPath = AssetDatabase.GUIDToAssetPath("c65be0434dc84fc4b58c57d82cb821fb");
                    var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);
                    if (styleSheet != null)
                    {
                        rootElement.styleSheets.Add(styleSheet);
                    }

                    // UI 생성
                    CreateSettingsUI(rootElement);
                }
            };

            return provider;
        }

        private static void CreateSettingsUI(VisualElement root)
        {
            var settings = Instance;
            var serializedObject = new SerializedObject(settings);

            // 컨테이너
            var container = new VisualElement();
            container.AddToClassList("settings-container");
            root.Add(container);

            // 제목
            var title = new Label("Native Gallery Settings");
            title.AddToClassList("settings-title");
            container.Add(title);

            // 구분선
            var separator = new VisualElement();
            separator.AddToClassList("settings-separator");
            container.Add(separator);

            // 자동 설정 토글
            var automatedSetupToggle = new Toggle("자동 설정")
            {
                value = settings.AutomatedSetup
            };
            automatedSetupToggle.AddToClassList("settings-toggle");
            automatedSetupToggle.RegisterValueChangedCallback(evt =>
            {
                settings.AutomatedSetup = evt.newValue;
                settings.Save();

                // 자식 요소들 활성화/비활성화
                UpdateFieldsState(container, evt.newValue);
            });
            container.Add(automatedSetupToggle);

            // iOS 설정 섹션
            var iosSection = new VisualElement();
            iosSection.AddToClassList("settings-section");
            container.Add(iosSection);

            var iosSectionLabel = new Label("iOS 권한 설정");
            iosSectionLabel.AddToClassList("settings-section-label");
            iosSection.Add(iosSectionLabel);

            // 사진 라이브러리 사용 설명
            var photoLibraryField = new TextField("사진 라이브러리 사용 설명")
            {
                value = settings.PhotoLibraryUsageDescription,
                multiline = true
            };
            photoLibraryField.AddToClassList("settings-textfield");
            photoLibraryField.RegisterValueChangedCallback(evt =>
            {
                settings.PhotoLibraryUsageDescription = evt.newValue;
                settings.Save();
            });
            iosSection.Add(photoLibraryField);

            // 사진 라이브러리 추가 기능 사용 설명
            var photoLibraryAdditionsField = new TextField("사진 라이브러리 추가 기능 사용 설명")
            {
                value = settings.PhotoLibraryAdditionsUsageDescription,
                multiline = true
            };
            photoLibraryAdditionsField.AddToClassList("settings-textfield");
            photoLibraryAdditionsField.RegisterValueChangedCallback(evt =>
            {
                settings.PhotoLibraryAdditionsUsageDescription = evt.newValue;
                settings.Save();
            });
            iosSection.Add(photoLibraryAdditionsField);

            // 제한된 사진 허가 토글
            var limitedPhotosToggle = new Toggle()
            {
                value = settings.DontAskLimitedPhotosPermissionAutomaticallyOnIos14,
                label = "제한된 사진 허가를 자동으로 요청하지 마세요",
                tooltip =
                    "See: https://mackuba.eu/2020/07/07/photo-library-changes-ios-14/. It's recommended to keep this setting enabled"
            };
            limitedPhotosToggle.AddToClassList("settings-toggle");
            limitedPhotosToggle.RegisterValueChangedCallback(evt =>
            {
                settings.DontAskLimitedPhotosPermissionAutomaticallyOnIos14 = evt.newValue;
                settings.Save();
            });
            iosSection.Add(limitedPhotosToggle);

            // 도움말 박스
            var helpBox = new HelpBox(
                "이 설정은 iOS 빌드 시 Info.plist에 자동으로 적용됩니다.",
                HelpBoxMessageType.Info
            );
            helpBox.AddToClassList("settings-helpbox");
            container.Add(helpBox);

            // 초기 상태 설정
            UpdateFieldsState(container, settings.AutomatedSetup);
        }

        private static void UpdateFieldsState(VisualElement container, bool enabled)
        {
            var section = container.Q<VisualElement>("settings-section");
            if (section == null)
            {
                // 클래스로 찾기
                var allElements = container.Query<VisualElement>(className: "settings-section").ToList();
                if (allElements.Count > 0)
                {
                    section = allElements[0];
                }
            }

            if (section != null)
            {
                section.SetEnabled(enabled);
            }
            else
            {
                // 직접 찾기
                var fields = container.Query<TextField>().ToList();
                var toggles = container.Query<Toggle>().ToList();

                foreach (var field in fields)
                {
                    field.SetEnabled(enabled);
                }

                // 첫 번째 토글(자동 설정)은 제외
                for (int i = 1; i < toggles.Count; i++)
                {
                    toggles[i].SetEnabled(enabled);
                }
            }
        }
    }
}