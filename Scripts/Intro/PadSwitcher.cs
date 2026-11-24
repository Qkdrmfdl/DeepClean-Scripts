using UnityEngine;
using System.Collections;

public class PadSwitcher : MonoBehaviour
{
    [Header("Pad Objects")]
    [SerializeField] private GameObject pad1;
    [SerializeField] private GameObject pad2;

    [Header("Timing Settings")]
    [SerializeField] private float switchDelay = 3f;
    [Tooltip("체크하면 타이머 자동 전환 비활성화 (IntroNarrationSequencer에서 제어할 때 사용)")]
    [SerializeField] private bool disableAutoSwitch = false;

    [Header("Optional: Sound")]
    [SerializeField] private AudioClip switchSound;
    [SerializeField][Range(0.1f, 5f)] private float soundVolume = 1.5f; // �ִ� 5����� ���� ����
    private AudioSource audioSource;

    void Start()
    {
        // AudioSource ���� (���尡 ���� ���)
        if (switchSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D ����
        }

        // �ʱ� ���� Ȯ��
        if (pad1 == null || pad2 == null)
        {
            Debug.LogError("Pad1 �Ǵ� Pad2�� �Ҵ���� �ʾҽ��ϴ�!");
            return;
        }

        // �ʱ� ���� ����
        pad1.SetActive(true);
        pad2.SetActive(false);

        if (disableAutoSwitch) return;

        Debug.Log($"{switchDelay}초 후에 Pad1에서 Pad2로 전환됩니다.");

        StartCoroutine(SwitchPadsAfterDelay());
    }

    IEnumerator SwitchPadsAfterDelay()
    {
        // ������ �ð���ŭ ���
        yield return new WaitForSeconds(switchDelay);

        // �е� ��ȯ
        SwitchPads();
    }

    void SwitchPads()
    {
        Debug.Log("Pad ��ȯ: Pad1 ��Ȱ��ȭ, Pad2 Ȱ��ȭ");

        // Pad1 ��Ȱ��ȭ
        if (pad1 != null)
        {
            pad1.SetActive(false);
        }

        // Pad2 Ȱ��ȭ
        if (pad2 != null)
        {
            pad2.SetActive(true);
        }

        // ���� ��� (���� ���)
        if (switchSound != null && audioSource != null)
        {
            // volume�� 1���� ũ�� AudioSource�� volume�� ����
            if (soundVolume > 1f)
            {
                audioSource.volume = Mathf.Min(soundVolume, 5f);
                audioSource.PlayOneShot(switchSound);
            }
            else
            {
                audioSource.PlayOneShot(switchSound, soundVolume);
            }
        }
    }

    // Inspector���� �������� ȣ���� �� �ִ� �׽�Ʈ �޼���
    [ContextMenu("Test Switch Pads Now")]
    void TestSwitchNow()
    {
        SwitchPads();
    }

    // ��ũ��Ʈ�� ���� �ð��� �����ϰ� ���� �� ���
    public void SetSwitchDelay(float newDelay)
    {
        switchDelay = newDelay;
    }

    // ��� ��ȯ�ϰ� ���� �� �ܺο��� ȣ�� ����
    public void SwitchPadsImmediately()
    {
        StopAllCoroutines(); // ���� �ڷ�ƾ ����
        SwitchPads();
    }
}