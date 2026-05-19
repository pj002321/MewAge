using UnityEngine;

/// <summary>
/// Inspector에서 Resources/Unit/Ch*/proj/ 폴더의 투사체 프리팹 드롭다운을 표시합니다.
/// string 필드에 붙여 사용합니다. 저장값은 Resources 상대 경로 (확장자 제외).
/// 예: "Unit/Ch1/proj/c1_proj_fire"
/// </summary>
public class ProjectileIDAttribute : PropertyAttribute { }
