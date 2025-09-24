using System.Collections.Generic;
using UnityEngine;

namespace Skills
{
    /// <summary>
    /// 캐릭터가 보유한 스킬들의 선택/사용/쿨타임을 관리합니다.
    /// - Player: Request(skillId) 호출로 사용
    /// - Bot: TryUseAnyUsable() 호출로 준비된 스킬을 자동 사용
    /// </summary>
    [DisallowMultipleComponent]
    public class SkillSelector : MonoBehaviour
    {
        [Tooltip("이 캐릭터가 보유한 스킬 데이터(ScriptableObject) 목록입니다.")]
        public SkillData[] ownedSkills;

        // 현재 캐릭터(플레이어나 봇)의 FSM 컨트롤러
        private CharacterControllerBaseFSM characterController;

        // 스킬별 런타임 상태(쿨타임, 시작시간 등) 보관
        private readonly Dictionary<string, SkillRuntime> skillRuntimes = new();

        private void Awake()
        {
            characterController = GetComponent<CharacterControllerBaseFSM>();
            if (characterController == null)
            {
                Debug.LogError("[SkillSelector] CharacterControllerBaseFSM가 필요합니다.");
                return;
            }

            if (ownedSkills == null) return;

            foreach (SkillData skillData in ownedSkills)
            {
                if (skillData == null || string.IsNullOrEmpty(skillData.id))
                    continue;

                // 스킬별 런타임 컨텍스트 초기화
                var runtime = new SkillRuntime(characterController, skillData);
                skillRuntimes[skillData.id] = runtime;
            }
        }

        /// <summary>
        /// 특정 스킬이 (쿨타임 등 조건을 만족하여) 지금 사용 가능한지 여부를 반환합니다.
        /// </summary>
        public bool IsReady(string skillId)
        {
            if (!skillRuntimes.TryGetValue(skillId, out SkillRuntime skillRuntime))
                return false;

            return Time.time >= skillRuntime.nextReadyTime;
        }

        /// <summary>
        /// 플레이어 입력 등으로 특정 스킬 사용을 요청합니다.
        /// 성공 시 SkillCastingState로 전환됩니다.
        /// </summary>
        public bool Request(string skillId)
        {
            if (!skillRuntimes.TryGetValue(skillId, out SkillRuntime skillRuntime))
                return false;

            ISkill skillImplementation = CreateSkillImplementation(skillRuntime.data.kind);
            if (skillImplementation == null)
                return false;

            float now = Time.time;

            // 스킬 고유의 사용 가능 로직(주로 쿨타임/중복 여부) 확인
            if (!skillImplementation.IsUsable(skillRuntime, now))
                return false;

            // 쿨타임 미종료면 실패
            if (now < skillRuntime.nextReadyTime)
                return false;

            // 캐스팅 시작: 공용 캐스팅 상태로 진입
            skillRuntime.castStartTime = now;
            characterController.ChangeState(new SkillCastingState(characterController, skillRuntime, skillImplementation));
            return true;
        }

        /// <summary>
        /// 봇용: 보유 스킬 중 "지금 사용 가능한" 첫 스킬을 즉시 사용합니다.
        /// (정책 단순화: 배열 순서 우선)
        /// </summary>
        public bool TryUseAnyUsable()
        {
            // 이미 스킬 캐스팅 중이면 아무것도 하지 않음
            if (characterController != null && characterController.IsCastingSkill())
                return false;
            
            // 보유 목록이 없으면 시도 불가
            if (ownedSkills == null || ownedSkills.Length == 0)
                return false;

            foreach (SkillData skillData in ownedSkills)
            {
                if (skillData == null || string.IsNullOrEmpty(skillData.id))
                    continue;

                if (!skillRuntimes.TryGetValue(skillData.id, out SkillRuntime skillRuntime))
                    continue;

                ISkill skillImplementation = CreateSkillImplementation(skillRuntime.data.kind);
                if (skillImplementation == null)
                    continue;

                float now = Time.time;
                if (now < skillRuntime.nextReadyTime)
                    continue;

                if (!skillImplementation.IsUsable(skillRuntime, now))
                    continue;

                skillRuntime.castStartTime = now;
                characterController.ChangeState(new SkillCastingState(characterController, skillRuntime, skillImplementation));
                return true;
            }

            return false;
        }

        /// <summary>
        /// 스킬 종류(SkillKind)에 따른 실제 구현 인스턴스를 생성합니다.
        /// 새 스킬을 추가할 때 여기에 매핑을 확장하세요.
        /// </summary>
        private ISkill CreateSkillImplementation(SkillKind kind)
        {
            switch (kind)
            {
                case SkillKind.MultiShot:
                    return new Skills.Impl.MultiShotSkill();
                case SkillKind.JumpShot:
                    return new Skills.Impl.JumpShotSkill();
                case SkillKind.TimeFreezeArrow:
                    return new Skills.Impl.TimeFreezeArrowSkill();
                case SkillKind.LightningArrow:
                    return new Skills.Impl.LightningArrowSkill();
                default:
                    return null;
            }
        }
        
        // UI에서 쿨다운 남은 시간을 읽기 위한 게터
        public float GetCooldownRemaining(string skillId)
        {
            if (!skillRuntimes.TryGetValue(skillId, out SkillRuntime r)) return 0f;
            return Mathf.Max(0f, r.nextReadyTime - Time.time);
        }

        // UI에서 총 쿨다운(초)을 읽기 위한 게터
        public float GetCooldownSeconds(string skillId)
        {
            if (!skillRuntimes.TryGetValue(skillId, out SkillRuntime r)) return 0f;
            return Mathf.Max(0f, r.data != null ? r.data.cooldownSeconds : 0f);
        }

        // 아이콘 스프라이트 반환
        public Sprite GetIcon(string skillId)
        {
            if (!skillRuntimes.TryGetValue(skillId, out SkillRuntime r)) return null;
            return r.data != null ? r.data.icon : null;
        }
    }
}
