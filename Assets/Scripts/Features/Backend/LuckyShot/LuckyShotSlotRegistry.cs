using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class LuckyShotRegisteredSlot
{
    public SlotScorer slotScorer;
    public StarPlate starPlate;
    public int boardNumber;
    public int slotIndex;
    public string slotId;
    public Transform slotTransform;
    public Transform highlightAnchor;
    public Collider slotCollider3D;

    public bool IsValid =>
        slotScorer != null &&
        starPlate != null &&
        slotTransform != null &&
        boardNumber >= 1 &&
        boardNumber <= 3 &&
        slotIndex >= 0 &&
        !string.IsNullOrWhiteSpace(slotId);

    public string CompositeKey => boardNumber + "::" + slotId;
}

public sealed class LuckyShotSlotRegistry : MonoBehaviour
{
    public static LuckyShotSlotRegistry Instance { get; private set; }

    [Header("Auto Scan")]
    [SerializeField] private Transform scanRoot;
    [SerializeField] private bool includeInactive = true;
    [SerializeField] private bool autoScanOnAwake = true;

    [Header("Optional anchor naming")]
    [SerializeField] private string highlightAnchorChildName = "HighlightAnchor";

    [Header("Debug")]
    [SerializeField] private bool verboseLogs = true;

    [Header("Runtime view - read only")]
    [SerializeField] private List<LuckyShotRegisteredSlot> slots = new List<LuckyShotRegisteredSlot>();

    private readonly Dictionary<string, LuckyShotRegisteredSlot> slotsByKey = new Dictionary<string, LuckyShotRegisteredSlot>();
    private readonly List<LuckyShotRegisteredSlot> board1Slots = new List<LuckyShotRegisteredSlot>();
    private readonly List<LuckyShotRegisteredSlot> board2Slots = new List<LuckyShotRegisteredSlot>();
    private readonly List<LuckyShotRegisteredSlot> board3Slots = new List<LuckyShotRegisteredSlot>();

    public IReadOnlyList<LuckyShotRegisteredSlot> AllSlots => slots;
    public IReadOnlyList<LuckyShotRegisteredSlot> Board1Slots => board1Slots;
    public IReadOnlyList<LuckyShotRegisteredSlot> Board2Slots => board2Slots;
    public IReadOnlyList<LuckyShotRegisteredSlot> Board3Slots => board3Slots;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (autoScanOnAwake)
            ScanScene();

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotSlotRegistry] Awake -> " +
                "Total=" + slots.Count +
                " | Board1=" + board1Slots.Count +
                " | Board2=" + board2Slots.Count +
                " | Board3=" + board3Slots.Count,
                this);
        }
    }

    public void ScanScene()
    {
        slots.Clear();
        slotsByKey.Clear();
        board1Slots.Clear();
        board2Slots.Clear();
        board3Slots.Clear();

        Transform root = scanRoot != null ? scanRoot : transform;

        SlotScorer[] found = root.GetComponentsInChildren<SlotScorer>(includeInactive);
        if (found == null || found.Length == 0)
        {
            if (verboseLogs)
                Debug.LogWarning("[LuckyShotSlotRegistry] ScanScene -> no SlotScorer found.", this);

            return;
        }

        for (int i = 0; i < found.Length; i++)
        {
            SlotScorer scorer = found[i];
            if (scorer == null)
                continue;

            StarPlate plate = scorer.parentPlate != null ? scorer.parentPlate : scorer.GetComponentInParent<StarPlate>();
            if (plate == null)
            {
                if (verboseLogs)
                    Debug.LogWarning("[LuckyShotSlotRegistry] Slot ignored because StarPlate is missing -> " + scorer.name, scorer);

                continue;
            }

            int boardNumber = Mathf.Clamp(plate.plateNumber, 1, 3);
            int slotIndex = Mathf.Max(0, scorer.slotIndex);
            string slotId = BuildSlotId(boardNumber, slotIndex);

            Transform anchor = ResolveHighlightAnchor(scorer.transform);
            Collider col3D = scorer.GetComponent<Collider>();

            LuckyShotRegisteredSlot entry = new LuckyShotRegisteredSlot
            {
                slotScorer = scorer,
                starPlate = plate,
                boardNumber = boardNumber,
                slotIndex = slotIndex,
                slotId = slotId,
                slotTransform = scorer.transform,
                highlightAnchor = anchor,
                slotCollider3D = col3D
            };

            if (!entry.IsValid)
            {
                if (verboseLogs)
                    Debug.LogWarning("[LuckyShotSlotRegistry] Invalid slot ignored -> " + scorer.name, scorer);

                continue;
            }

            if (slotsByKey.ContainsKey(entry.CompositeKey))
            {
                if (verboseLogs)
                    Debug.LogWarning("[LuckyShotSlotRegistry] Duplicate slot key ignored -> " + entry.CompositeKey, scorer);

                continue;
            }

            slots.Add(entry);
            slotsByKey.Add(entry.CompositeKey, entry);

            switch (entry.boardNumber)
            {
                case 1:
                    board1Slots.Add(entry);
                    break;
                case 2:
                    board2Slots.Add(entry);
                    break;
                case 3:
                    board3Slots.Add(entry);
                    break;
            }

            if (verboseLogs)
            {
                Debug.Log(
                    "[LuckyShotSlotRegistry] Registered -> " +
                    "Board=" + entry.boardNumber +
                    " | SlotIndex=" + entry.slotIndex +
                    " | SlotId=" + entry.slotId +
                    " | Object=" + entry.slotTransform.name,
                    entry.slotTransform);
            }
        }

        SortBoardSlots(board1Slots);
        SortBoardSlots(board2Slots);
        SortBoardSlots(board3Slots);
    }

    public LuckyShotRegisteredSlot GetSlot(int boardNumber, int slotIndex)
    {
        string key = BuildSlotId(boardNumber, slotIndex);
        slotsByKey.TryGetValue(boardNumber + "::" + key, out LuckyShotRegisteredSlot entry);
        return entry;
    }

    public LuckyShotRegisteredSlot GetSlot(int boardNumber, string slotId)
    {
        if (string.IsNullOrWhiteSpace(slotId))
            return null;

        slotsByKey.TryGetValue(boardNumber + "::" + slotId, out LuckyShotRegisteredSlot entry);
        return entry;
    }

    public List<LuckyShotRegisteredSlot> GetSlotsForBoard(int boardNumber)
    {
        switch (boardNumber)
        {
            case 1: return new List<LuckyShotRegisteredSlot>(board1Slots);
            case 2: return new List<LuckyShotRegisteredSlot>(board2Slots);
            case 3: return new List<LuckyShotRegisteredSlot>(board3Slots);
            default: return new List<LuckyShotRegisteredSlot>();
        }
    }

    public LuckyShotRegisteredSlot GetRandomSlotForBoard(int boardNumber, System.Random rng = null)
    {
        List<LuckyShotRegisteredSlot> list = GetSlotsForBoard(boardNumber);
        if (list.Count == 0)
            return null;

        int index = rng != null ? rng.Next(0, list.Count) : UnityEngine.Random.Range(0, list.Count);
        return list[index];
    }

    public int GetBoardSlotCount(int boardNumber)
    {
        switch (boardNumber)
        {
            case 1: return board1Slots.Count;
            case 2: return board2Slots.Count;
            case 3: return board3Slots.Count;
            default: return 0;
        }
    }

    public static string BuildSlotId(int boardNumber, int slotIndex)
    {
        return "b" + Mathf.Clamp(boardNumber, 1, 3) + "_slot_" + slotIndex.ToString("00");
    }

    private Transform ResolveHighlightAnchor(Transform slotTransform)
    {
        if (slotTransform == null)
            return null;

        if (!string.IsNullOrWhiteSpace(highlightAnchorChildName))
        {
            Transform child = slotTransform.Find(highlightAnchorChildName);
            if (child != null)
                return child;
        }

        return slotTransform;
    }

    private static void SortBoardSlots(List<LuckyShotRegisteredSlot> list)
    {
        list.Sort((a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            return a.slotIndex.CompareTo(b.slotIndex);
        });
    }

#if UNITY_EDITOR
    [ContextMenu("Scan Scene Now")]
    private void EditorScanSceneNow()
    {
        ScanScene();
    }
#endif
}
