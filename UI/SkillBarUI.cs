using System.Collections.Generic;
using UnityEngine;

namespace Skills.UI
{
    [DisallowMultipleComponent]
    public class SkillBarUI : MonoBehaviour
    {
        [Header("References")]
        public Skills.SkillSelector selector;   // Player의 SkillSelector
        public SkillButtonUI buttonPrefab;      // 프리팹(SkillButtonUI 스크립트 필수)
        public Transform container;             // 없으면 this.transform

        [Header("Config")]
        public string[] skillIds;               // ex) "skill.multishot", "skill.jumpshot"
        public bool clearExisting = true;

        private readonly List<SkillButtonUI> _spawned = new();

        void Awake()
        {
            if (!container) container = transform;
        }

        void Start()
        {
            Build();
        }

        public void Build()
        {
            if (!buttonPrefab)
            {
                Debug.LogError("[SkillBarUI] buttonPrefab이 비어있습니다. SkillButtonUI 타입 프리팹을 배치하세요.");
                return;
            }

            if (clearExisting)
            {
                for (int i = container.childCount - 1; i >= 0; --i)
                    Destroy(container.GetChild(i).gameObject);
                _spawned.Clear();
            }

            if (skillIds == null || skillIds.Length == 0)
            {
                Debug.LogWarning("[SkillBarUI] skillIds가 비어있습니다.");
                return;
            }

            foreach (var id in skillIds)
            {
                if (string.IsNullOrWhiteSpace(id)) continue;

                // 컴포넌트 타입으로 바로 인스턴스화 → SkillButtonUI 반환
                var ui = Instantiate(buttonPrefab, container);
                ui.name = $"SkillButton_{id}";

                ui.selector = selector;
                ui.skillId = id;

                // 아이콘 미리 바인딩(선택)
                if (selector != null && ui.icon != null)
                {
                    var spr = selector.GetIcon(id);
                    if (spr) ui.icon.sprite = spr;
                }

                _spawned.Add(ui);
            }
        }

        public void Rebuild(string[] newSkillIds)
        {
            skillIds = newSkillIds;
            Build();
        }
    }
}
