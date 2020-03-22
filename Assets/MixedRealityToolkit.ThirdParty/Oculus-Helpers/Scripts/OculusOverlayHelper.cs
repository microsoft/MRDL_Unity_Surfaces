using System.Collections;
using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;

namespace prvncher.OculusHelpers
{
    public class OculusOverlayHelper : MonoBehaviour
    {
        [SerializeField]
        private OVROverlay overlayInstance = null;

        public void SetOverlayActive(bool isActive)
        {
            if (overlayInstance != null)
            {
                UpdateOverlayPos();
                overlayInstance.enabled = isActive;
            }
        }

        private void Awake()
        {
            SetOverlayActive(true);
        }

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(2f);
            SetOverlayActive(false);
        }

        private void UpdateOverlayPos()
        {
            if (overlayInstance != null && overlayInstance.isActiveAndEnabled)
            {
                Vector3 projectedForward = Vector3.ProjectOnPlane(CameraCache.Main.transform.forward, Vector3.up);
                overlayInstance.transform.position = CameraCache.Main.transform.position + projectedForward - Vector3.up * 0.5f;
                overlayInstance.transform.rotation = Quaternion.LookRotation(projectedForward, Vector3.up);
                overlayInstance.transform.localScale = Vector3.one * 2f;
            }
        }

        private void Update()
        {
            UpdateOverlayPos();
        }

        private void OnDestroy()
        {
            SetOverlayActive(false);
            Destroy(overlayInstance.gameObject);
        }
    }
}