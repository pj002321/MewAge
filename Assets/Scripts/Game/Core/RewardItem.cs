using Mewage.DB;
using UnityEngine;

/// <summary>
/// 보상 타입 열거형. RewardManager의 지급 분기에 사용.
/// </summary>
public enum RewardType
{
    Gold, Exp, Ruby, CharMat,
    EquipTopMat, EquipcloakMat, EquipRingMat, EquipNeckMat, EquipFootMat, EquipHeadMat,
    RelicScroll,
    EquipFrag, WareFrag,
    TicketEqNormal, TicketEqRare, TicketEqEpic,
    TicketRfNormal, TicketRfEpic,
}

/// <summary>
/// 단일 보상 단위.
/// rewardId는 DB_상점 f_보상ID 원본 문자열을 보존하여
/// RewardVisualConfig.GetIcon(rewardId)로 직접 아이콘을 조회합니다.
/// </summary>
public struct RewardItem
{
    public string     rewardId; // DB f_보상ID 원본 ("RWD_GOLD" 등)
    public RewardType type;     // 지급 로직 분기용
    public int        amount;

    public RewardItem(string rewardId, int amount)
    {
        this.rewardId = rewardId;
        this.type     = ParseId(rewardId);
        this.amount   = amount;
    }

    public RewardItem(RewardType type, int amount)
    {
        this.type     = type;
        this.amount   = amount;
        this.rewardId = "";  // enum 경로로 생성 시 rewardId는 빈 문자열
    }

    /// <summary>DB_상점 f_보상ID 문자열을 RewardType으로 변환.</summary>
    public static RewardType ParseId(string id) => id switch
    {
        "COIN"            => RewardType.Gold,
        "RUBY"            => RewardType.Ruby,
        "EXP"             => RewardType.Exp,
        "MAT_HERO"        => RewardType.CharMat,
        "MAT_RELIC"       => RewardType.RelicScroll,
        "MAT_EQ_TOP"      => RewardType.EquipTopMat,
        "MAT_EQ_HEAD"     => RewardType.EquipHeadMat,
        "MAT_EQ_RING"     => RewardType.EquipRingMat,
        "MAT_EQ_NECK"     => RewardType.EquipNeckMat,
        "MAT_EQ_CLOAK"    => RewardType.EquipcloakMat,
        "MAT_EQ_FOOT"     => RewardType.EquipFootMat,
        "KEY_NORMAL"      => RewardType.TicketEqNormal,
        "KEY_EPIC"        => RewardType.TicketEqEpic,
        "KEY_SPECIAL"     => RewardType.TicketEqRare,
        "KEY_RELIC_NORMAL"=> RewardType.TicketRfNormal,
        "KEY_RELIC_EPIC"  => RewardType.TicketRfEpic,
        _                 => RewardType.Gold,
    };

    /// <summary>
    /// DB_상점 행에서 RewardItem을 생성. 수량은 f_V1(최소)~f_V2(최대) 범위에서 랜덤.
    /// f_V2==0이면 f_수량을 고정값으로 사용.
    /// BansheeGz 필드 임시 매핑: f_V1=수량최소 f_V2=수량최대 f_V3=웨이브 f_V4=챕터 f_T1=통화 f_S가격=일일한도
    /// </summary>
    public static RewardItem FromDB(DB_상점 row)
    {
        int min = row.f_V1;
        int max = row.f_V2;
        int amount;
        if (max > min && max > 0)
            amount = Random.Range(min, max + 1);
        else if (min > 0)
            amount = min;
        else
            amount = row.f_수량;
        return new RewardItem(row.f_보상ID, Mathf.Max(1, amount));
    }
}
