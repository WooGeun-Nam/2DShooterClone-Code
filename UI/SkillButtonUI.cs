using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Skills.UI
{
    /// <summary>
    /// 단일 스킬 버튼 UI:
    /// - OnClick → SkillSelector.Request(skillId)
    /// - 라디얼/숫자 쿨다운 표시
    /// - 아이콘 바인딩
    /// </summary>
    [DisallowMultipleComponent]
    public class SkillButtonUI : MonoBehaviour
    {
        [Header("Binding")]
        public Skills.SkillSelector selector;   // Player의 SkillSelector
        public string skillId;                  // ex) "skill.multishot", "skill.jumpshot"

        [Header("UI References (children)")]
        public Image icon;            // ButtonSkill/Icon
        public Image cooldownRadial;  // ButtonSkill/CooldownRadial (Image Type=Filled, Radial360)
        public TMP_Text cooldownText; // ButtonSkill/CooldownText
        public Button button;         // ButtonSkill

        [Header("Options")]
        [Tooltip("라디얼이 1에서 0으로 줄어드는 방향이면 true")]
        public bool radialCountDown = true;

        void Reset()
        {
            // 하이어라키 이름 기준으로 자동 바인딩
            icon           = transform.Find("Icon")?.GetComponent<Image>();
            cooldownRadial = transform.Find("CooldownRadial")?.GetComponent<Image>();
            cooldownText   = transform.Find("CooldownText")?.GetComponent<TMP_Text>();
            button         = GetComponent<Button>();
        }

        void Awake()
        {
            // 누락시 한 번 더 안전하게 탐색
            if (!icon)           icon           = transform.Find("Icon")?.GetComponent<Image>();
            if (!cooldownRadial) cooldownRadial = transform.Find("CooldownRadial")?.GetComponent<Image>();
            if (!cooldownText)   cooldownText   = transform.Find("CooldownText")?.GetComponent<TMP_Text>();
            if (!button)         button         = GetComponent<Button>();

            if (button)
                button.onClick.AddListener(OnClick);
        }

        void OnEnable()
        {
            // 최초 아이콘 바인딩(Selector가 준비돼 있으면)
            TryBindIcon();
            // Radial 타입 미설정 시 안전장치
            if (cooldownRadial && cooldownRadial.type != Image.Type.Filled)
                cooldownRadial.type = Image.Type.Filled;
        }

        void Update()
        {
            if (selector == null || string.IsNullOrEmpty(skillId))
            {
                SetInteractable(false);
                SetCooldownVisual(0f, 1f); // 비표시
                return;
            }

            // 남은 시간/총 쿨타임
            float remain = selector.GetCooldownRemaining(skillId);   // [uses added getter]
            float total  = selector.GetCooldownSeconds(skillId);     // [uses added getter]

            bool ready = remain <= 0.001f || total <= 0.001f;

            // 버튼 상호작용
            SetInteractable(ready);

            // 라디얼/텍스트
            float t = ready ? 0f : Mathf.Clamp01(remain / Mathf.Max(0.0001f, total));
            SetCooldownVisual(remain, t);

            // 아이콘이 비어 있으면 한 번 더 바인딩 시도
            if (!icon || icon.sprite == null)
                TryBindIcon();
        }

        private void OnClick()
        {
            if (selector == null || string.IsNullOrEmpty(skillId)) return;
            selector.Request(skillId); // 성공 시 캐스팅 상태로 전환됨 :contentReference[oaicite:3]{index=3}
        }

        private void SetInteractable(bool v)
        {
            if (button) button.interactable = v;
        }

        private void SetCooldownVisual(float remainSeconds, float normalized)
        {
            if (cooldownRadial)
                cooldownRadial.fillAmount = radialCountDown ? normalized : (1f - normalized);

            if (cooldownText)
                cooldownText.text = (remainSeconds <= 0.001f) ? "" : Mathf.CeilToInt(remainSeconds).ToString();
        }

        private void TryBindIcon()
        {
            if (selector == null || string.IsNullOrEmpty(skillId) || icon == null) return;
            var spr = selector.GetIcon(skillId);
            if (spr != null) icon.sprite = spr;
        }
    }
}
