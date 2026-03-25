using Mewage.DB;

/// <summary>
/// DB_장비 행의 런타임 복사본.
/// 등급: "노말" / "레어" / "에픽" / "S급"
/// </summary>
public class EquipmentData
{
    public string name;
    public string grade;    // "노말" / "레어" / "에픽" / "S급"
    public int    atk;
    public int    evade;
    public int    def;
    public int    hp;
    public int    atkSpeed;
    public int    crit;

    public static EquipmentData FromDB(DB_장비 row) => new EquipmentData
    {
        name     = row.f_이름,
        grade    = row.f_등급,
        atk      = row.f_공격력,
        evade    = row.f_회피,
        def      = row.f_방어력,
        hp       = row.f_체력,
        atkSpeed = row.f_공격속도,
        crit     = row.f_치명타,
    };
}
