using System;
using System.Collections.Generic;
using Nyoice.NPC;
using UnityEngine;

namespace Nyoice.Managers
{
    [DisallowMultipleComponent]
    public sealed class UrinalTicketManager : MonoBehaviour
    {
        [SerializeField, Min(1)]
        private int totalTicketCount = 8;

        [SerializeField]
        private bool enableDebugLogs = true;

        [SerializeField]
        private GameStateManager gameStateManager;

        private readonly HashSet<NPCController> _ticketHolders = new HashSet<NPCController>();

        public event Action TicketReleased;

        public int TotalTicketCount => totalTicketCount;
        public int UsedTicketCount => _ticketHolders.Count;
        public int AvailableTicketCount => Mathf.Max(0, totalTicketCount - UsedTicketCount);
        public bool CanAcquireTickets => gameStateManager == null || !gameStateManager.IsGameOver;

        public void Configure(int ticketCount)
        {
            totalTicketCount = Mathf.Max(1, ticketCount);
        }

        public void ConfigureGameState(GameStateManager configuredGameStateManager)
        {
            gameStateManager = configuredGameStateManager;
        }

        public bool TryAcquireTicket(NPCController npc)
        {
            if (!CanAcquireTickets || npc == null ||
                _ticketHolders.Contains(npc) || AvailableTicketCount == 0)
            {
                return false;
            }

            _ticketHolders.Add(npc);
            Log($"{npc.name} acquired UrinalTicket");
            return true;
        }

        public bool ReleaseTicket(NPCController npc)
        {
            if (npc == null || !_ticketHolders.Remove(npc))
            {
                return false;
            }

            Log($"{npc.name} released UrinalTicket");
            TicketReleased?.Invoke();
            return true;
        }

        public bool HasTicket(NPCController npc)
        {
            return npc != null && _ticketHolders.Contains(npc);
        }

        private void Log(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log(message, this);
            }
        }
    }
}
