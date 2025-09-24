using UnityEngine;

namespace Skills
{
    /// <summary>
    /// 개별 스킬의 실행 전략 인터페이스(멀티샷, 점프샷 등).
    /// </summary>
    public interface ISkill
    {
        /// <summary> 지금 사용 가능한지(쿨타임/중복 여부 등) 판단합니다. </summary>
        bool IsUsable(SkillRuntime skillRuntime, float now);

        /// <summary> 캐스팅 시작 시 호출됩니다(애니 트리거/사운드 등 초기 처리). </summary>
        void BeginCast(SkillRuntime skillRuntime);

        /// <summary> 임팩트 타이밍(애니 이벤트 or 타임아웃)에 호출됩니다. </summary>
        void OnImpact(SkillRuntime skillRuntime);

        /// <summary> 캐스팅 종료 시 호출됩니다(쿨타임 시작 등 정리). </summary>
        void EndCast(SkillRuntime skillRuntime);
    }
}