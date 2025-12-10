using System.Collections.Generic;
using UnityEngine;

public class PanelBillboardManager : MonoBehaviour
{
    [Header("Camera to Face (defaults to Main Camera)")]
    [SerializeField] private Camera targetCamera;

    [Header("Panels that should face the camera")]
    [SerializeField] private Transform[] panels;

    [Header("Rotation Offset (use this if panels aren't perfectly facing)")]
    [SerializeField] private float pitchOffset = -90f; // X
    [SerializeField] private float yawOffset = 0f;   // Y
    [SerializeField] private float rollOffset = 0f;   // Z

    private void Awake()
    {
        // Default to main camera if none assigned
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        // Optional: auto-fill from children if array is empty
        if ((panels == null || panels.Length == 0) && transform.childCount > 0)
        {
            AutoFillPanelsFromChildren();
        }
    }

    private void LateUpdate()
    {
        if (targetCamera == null) return;

        Transform camTransform = targetCamera.transform;

        foreach (Transform panel in panels)
        {
            if (panel == null) continue;

            // Direction from panel to camera
            Vector3 toCamera = (camTransform.position - panel.position).normalized;
            if (toCamera.sqrMagnitude < 0.0001f) continue;

            // Base rotation that faces the camera
            Quaternion lookRot = Quaternion.LookRotation(toCamera, Vector3.up);

            // Apply correction for mesh orientation
            Quaternion offsetRot = Quaternion.Euler(pitchOffset, yawOffset, rollOffset);

            panel.rotation = lookRot * offsetRot;
        }
    }

    // Fill 'panels' with this object's direct children
    [ContextMenu("Auto Fill Panels From Children")]
    private void AutoFillPanelsFromChildren()
    {
        List<Transform> list = new List<Transform>();

        foreach (Transform child in transform)
        {
            list.Add(child);
        }

        panels = list.ToArray();
    }
}
