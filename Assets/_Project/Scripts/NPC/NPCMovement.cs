using System;
using UnityEngine;

namespace Nyoice.NPC
{
    /// <summary>
    /// Moves an NPC directly toward a target without navigation packages.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NPCMovement : MonoBehaviour
    {
        [SerializeField, Min(0.1f)]
        private float speed = 2.5f;

        private const float ArrivalDistance = 0.01f;

        private Vector3 _targetPosition;
        private Action _onArrived;

        public bool IsMoving { get; private set; }

        public void MoveTo(Vector3 targetPosition, Action onArrived)
        {
            _targetPosition = targetPosition;
            _onArrived = onArrived;
            IsMoving = true;
        }

        private void Update()
        {
            if (!IsMoving)
            {
                return;
            }

            transform.position = Vector3.MoveTowards(
                transform.position,
                _targetPosition,
                speed * Time.deltaTime);

            if (Vector3.Distance(transform.position, _targetPosition) > ArrivalDistance)
            {
                return;
            }

            transform.position = _targetPosition;
            IsMoving = false;

            Action onArrived = _onArrived;
            _onArrived = null;
            onArrived?.Invoke();
        }
    }
}
