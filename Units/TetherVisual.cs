using UnityEngine;

namespace Dokkaebi.Units
{
    public class TetherVisual : MonoBehaviour
    {
        public DokkaebiUnit Unit1;
        public DokkaebiUnit Unit2;
        private LineRenderer lineRenderer;

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                Debug.LogWarning("[TetherVisual] No LineRenderer component found on GameObject.", this);
            }
        }

        private void Update()
        {
            if (lineRenderer != null && Unit1 != null && Unit2 != null)
            {
                lineRenderer.positionCount = 2;
                lineRenderer.SetPosition(0, Unit1.transform.position);
                lineRenderer.SetPosition(1, Unit2.transform.position);
            }
        }
    }
} 