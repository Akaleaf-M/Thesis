using UnityEngine;

public class FragmentSlotTester : MonoBehaviour
{
    public FragmentSlot slot;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("Space pressed");

            if (slot != null)
            {
                Debug.Log("Activating slot");

                slot.Activate(
                    "mixamorig:Spine2",
                    new Vector3(0f, 0f, 0f),
                    new Vector3(1.5f, 0.5f, 0f)
                );
            }
            else
            {
                Debug.Log("Slot is NULL");
            }
        }
    }
}