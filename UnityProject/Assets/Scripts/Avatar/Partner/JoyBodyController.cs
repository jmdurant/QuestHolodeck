using UnityEngine;

public class JoyBodyController : PartnerBodyController
{
    [Header("Joy Model")]
    public Transform modelRoot;
    public string modelRootName = "JoyPartner";

    protected override void Awake()
    {
        AutoBind();
        base.Awake();

        avatarDriver ??= GetComponent<SexKitAvatarDriver>();
        if (avatarDriver != null && avatarDriver.bodyBRoot != null)
        {
            avatarDriver.bodyBRoot.gameObject.SetActive(false);
        }

        var voiceController = FindFirstObjectByType<PartnerVoiceController>();
        if (voiceController != null && headBone != null)
        {
            voiceController.followTarget = headBone;
        }
    }

    protected override void ResolvePartnerRoot()
    {
        if (partnerRoot == null && modelRoot != null)
        {
            partnerRoot = modelRoot;
            return;
        }

        base.ResolvePartnerRoot();
    }

    private void AutoBind()
    {
        if (modelRoot == null)
        {
            var directModel = transform.Find(modelRootName);
            if (directModel != null)
            {
                modelRoot = directModel;
            }
        }

        if (modelRoot == null)
        {
            return;
        }

        partnerRoot = modelRoot;
        headBone ??= FindBone("DEF-spine.006");
        chestBone ??= FindBone("spine_fk.003") ?? FindBone("spine_fk.002");
        leftHandBone ??= FindBone("DEF-hand.L");
        rightHandBone ??= FindBone("DEF-hand.R");
    }

    private Transform FindBone(string boneName)
    {
        if (modelRoot == null)
        {
            return null;
        }

        var transforms = modelRoot.GetComponentsInChildren<Transform>(true);
        foreach (var current in transforms)
        {
            if (current.name == boneName)
            {
                return current;
            }
        }

        return null;
    }
}
