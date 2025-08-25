using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class VRSeatAligner : MonoBehaviour
{
    public Transform driverSeat; //driverseat
    public Transform xrOrigin;   // XR Origin (root of rig)
    public Transform headCamera; // the XR camera (Main Camera inside XR Origin)

    void Start()
    {
        Vector3 offset = xrOrigin.position - headCamera.position;
        xrOrigin.position = driverSeat.position + offset;
    }
}
