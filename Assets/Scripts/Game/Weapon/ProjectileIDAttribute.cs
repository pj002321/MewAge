using UnityEngine;

/// <summary>
/// Inspector에서 Resources 하위 proj/ 폴더의 투사체 프리팹 드롭다운을 표시합니다.
/// rootFolder 생략 시 "Unit" (보스·적 투사체), "Weapons" 지정 시 무기 투사체 폴더를 탐색합니다.
/// 저장값은 Resources 상대 경로 (확장자 제외).
/// </summary>
public class ProjectileIDAttribute : PropertyAttribute
{
    public readonly string RootFolder;
    public ProjectileIDAttribute(string rootFolder = "Unit") => RootFolder = rootFolder;
}
