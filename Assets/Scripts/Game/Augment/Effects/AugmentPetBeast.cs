using UnityEngine;

// 애완동물 1마리를 소환한다. 플레이어를 따라다니며 적을 공격한다.
// 프리팹 필요: Resources/Augment/PetBeast
public class AugmentPetBeast : AugmentEffectBase
{
    private GameObject _pet;

    public override void OnGain()
    {
        var prefab = ImprovVisualConfig.Shared?.GetEntry(ImprovType.AUG_PET_BEAST).companionPrefab;
        if (prefab == null) return;

        var pos = PlayerController.Instance != null
            ? (Vector2)PlayerController.Instance.transform.position + Vector2.right
            : Vector2.zero;

        _pet = Object.Instantiate(prefab, pos, Quaternion.identity);
        _pet.GetComponent<CompanionController>()?.Init(
            damage:   Data.f_주수치,
            interval: Data.f_보조수치,
            range:    4f,
            follow:   true);
    }

    public override void OnRemove()
    {
        if (_pet != null) Object.Destroy(_pet);
    }
}
