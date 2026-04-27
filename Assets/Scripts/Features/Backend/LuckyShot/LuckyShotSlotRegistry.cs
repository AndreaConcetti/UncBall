using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class LuckyShotSlotRegistry : MonoBehaviour
{
    [Serializable]
    public sealed class LuckyShotRegisteredSlot
    {
        public int boardNumber;
        public string slotId;
        public int slotIndex;
        public Transform slotTransform;
        public Transform highlightAnchor;
        public Collider hitCollider3D;
        public Collider2D hitCollider2D;
        public SlotScorer slotScorer;
        public LuckyShotSlotTarget slotTarget;
    }

    [Header("Scan")]
    [SerializeField] private bool autoScanOnAwake = true;
    [SerializeField] private Transform scanRoot;
    [SerializeField] private bool includeInactive = true;

    [Header("Naming")]
    [SerializeField] private string highlightAnchorChildName = "HighlightAnchor";

    [Header("Debug")]
    [SerializeField] private bool verboseLogs = true;

    [SerializeField] private List<LuckyShotRegisteredSlot> slots = new List<LuckyShotRegisteredSlot>();

    private readonly Dictionary<string, LuckyShotRegisteredSlot> byBoardAndId = new Dictionary<string, LuckyShotRegisteredSlot>();
    private readonly Dictionary<int, List<LuckyShotRegisteredSlot>> byBoard = new Dictionary<int, List<LuckyShotRegisteredSlot>>();

    public IReadOnlyList<LuckyShotRegisteredSlot> Slots => slots;

    private void Awake()
    {
        if (autoScanOnAwake)
            ScanScene();

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotSlotRegistry] Awake -> Total=" + slots.Count +
                " | Board1=" + GetBoardCount(1) +
                " | Board2=" + GetBoardCount(2) +
                " | Board3=" + GetBoardCount(3),
                this);
        }
    }

    [ContextMenu("Scan Scene")]
    public void ScanScene()
    {
        slots.Clear();
        byBoardAndId.Clear();
        byBoard.Clear();

        Transform root = scanRoot != null ? scanRoot : transform.root;

        LuckyShotSlotTarget[] slotTargets = includeInactive
            ? root.GetComponentsInChildren<LuckyShotSlotTarget>(true)
            : root.GetComponentsInChildren<LuckyShotSlotTarget>(false);

        if (slotTargets != null && slotTargets.Length > 0)
        {
            for (int i = 0; i < slotTargets.Length; i++)
                RegisterFromLuckyShotTarget(slotTargets[i]);

            SortLists();

            if (verboseLogs)
                Debug.Log("[LuckyShotSlotRegistry] ScanScene -> registered from LuckyShotSlotTarget. Total=" + slots.Count, this);

            return;
        }

        SlotScorer[] scorers = includeInactive
            ? root.GetComponentsInChildren<SlotScorer>(true)
            : root.GetComponentsInChildren<SlotScorer>(false);

        if (scorers == null || scorers.Length == 0)
        {
            Debug.LogWarning("[LuckyShotSlotRegistry] ScanScene -> no LuckyShotSlotTarget and no SlotScorer found.", this);
            return;
        }

        for (int i = 0; i < scorers.Length; i++)
            RegisterFromSlotScorer(scorers[i]);

        SortLists();

        if (verboseLogs)
            Debug.Log("[LuckyShotSlotRegistry] ScanScene -> fallback SlotScorer registration. Total=" + slots.Count, this);
    }

    public LuckyShotRegisteredSlot GetSlot(int boardNumber, string slotId)
    {
        if (string.IsNullOrWhiteSpace(slotId))
            return null;

        byBoardAndId.TryGetValue(BuildKey(boardNumber, slotId), out LuckyShotRegisteredSlot slot);
        return slot;
    }

    public LuckyShotRegisteredSlot GetRandomSlotForBoard(int boardNumber)
    {
        if (!byBoard.TryGetValue(boardNumber, out List<LuckyShotRegisteredSlot> list))
            return null;

        if (list == null || list.Count == 0)
            return null;

        int index = UnityEngine.Random.Range(0, list.Count);
        return list[index];
    }

    public int GetBoardCount(int boardNumber)
    {
        if (!byBoard.TryGetValue(boardNumber, out List<LuckyShotRegisteredSlot> list))
            return 0;

        return list.Count;
    }

    private void RegisterFromLuckyShotTarget(LuckyShotSlotTarget target)
    {
        if (target == null)
            return;

        int boardNumber = MapBoardTier(target.BoardTier);

        // Se LuckyShotSlotTarget ha già uno slotId esplicito lo usa direttamente,
        // altrimenti lo costruisce dall'indice (che ora parte da 0).
        string finalSlotId = string.IsNullOrWhiteSpace(target.SlotId)
            ? BuildSlotId(target.SlotOrderInBoard)
            : target.SlotId.Trim();

        int slotIndex = Mathf.Max(0, target.SlotOrderInBoard);

        Transform slotTransform = target.transform;
        Transform highlightAnchor = target.HighlightAnchor != null
            ? target.HighlightAnchor
            : FindChildRecursive(slotTransform, highlightAnchorChildName);

        Collider hit3D = target.HitCollider3D != null ? target.HitCollider3D : target.GetComponent<Collider>();
        Collider2D hit2D = target.HitCollider2D != null ? target.HitCollider2D : target.GetComponent<Collider2D>();

        LuckyShotRegisteredSlot slot = new LuckyShotRegisteredSlot
        {
            boardNumber = boardNumber,
            slotId = finalSlotId,
            slotIndex = slotIndex,
            slotTransform = slotTransform,
            highlightAnchor = highlightAnchor != null ? highlightAnchor : slotTransform,
            hitCollider3D = hit3D,
            hitCollider2D = hit2D,
            slotScorer = target.GetComponent<SlotScorer>(),
            slotTarget = target
        };

        AddSlot(slot);
    }

    private void RegisterFromSlotScorer(SlotScorer scorer)
    {
        if (scorer == null)
            return;

        Transform slotTransform = scorer.transform;
        StarPlate plate = scorer.GetComponentInParent<StarPlate>(true);

        int boardNumber = plate != null ? plate.plateNumber : InferBoardNumberFromHierarchy(slotTransform);

        // FIX: InferSlotIndexFromName ora gestisce nomi che partono da 0 (Slot0, Slot1...).
        int slotIndex = InferSlotIndexFromName(slotTransform.name);

        // L'id corrisponde direttamente all'indice estratto dal nome.
        string finalSlotId = BuildSlotId(slotIndex);

        Transform highlightAnchor = FindChildRecursive(slotTransform, highlightAnchorChildName);

        LuckyShotRegisteredSlot slot = new LuckyShotRegisteredSlot
        {
            boardNumber = boardNumber,
            slotId = finalSlotId,
            slotIndex = slotIndex,
            slotTransform = slotTransform,
            highlightAnchor = highlightAnchor != null ? highlightAnchor : slotTransform,
            hitCollider3D = scorer.GetComponent<Collider>(),
            hitCollider2D = scorer.GetComponent<Collider2D>(),
            slotScorer = scorer,
            slotTarget = scorer.GetComponent<LuckyShotSlotTarget>()
        };

        AddSlot(slot);
    }

    private void AddSlot(LuckyShotRegisteredSlot slot)
    {
        if (slot == null)
            return;

        if (slot.boardNumber <= 0)
        {
            Debug.LogWarning("[LuckyShotSlotRegistry] AddSlot skipped -> invalid board number for " + GetTransformName(slot.slotTransform), this);
            return;
        }

        string key = BuildKey(slot.boardNumber, slot.slotId);
        if (byBoardAndId.ContainsKey(key))
        {
            Debug.LogWarning("[LuckyShotSlotRegistry] Duplicate slot skipped -> " + key, this);
            return;
        }

        slots.Add(slot);
        byBoardAndId.Add(key, slot);

        if (!byBoard.TryGetValue(slot.boardNumber, out List<LuckyShotRegisteredSlot> list))
        {
            list = new List<LuckyShotRegisteredSlot>();
            byBoard.Add(slot.boardNumber, list);
        }

        list.Add(slot);

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotSlotRegistry] Registered -> Board=" + slot.boardNumber +
                " | SlotId=" + slot.slotId +
                " | SlotIndex=" + slot.slotIndex +
                " | Transform=" + GetTransformName(slot.slotTransform),
                this);
        }
    }

    private void SortLists()
    {
        slots.Sort(CompareSlots);

        foreach (KeyValuePair<int, List<LuckyShotRegisteredSlot>> kv in byBoard)
            kv.Value.Sort(CompareSlots);
    }

    private int CompareSlots(LuckyShotRegisteredSlot a, LuckyShotRegisteredSlot b)
    {
        int boardCompare = a.boardNumber.CompareTo(b.boardNumber);
        if (boardCompare != 0)
            return boardCompare;

        return a.slotIndex.CompareTo(b.slotIndex);
    }

    private static string BuildKey(int boardNumber, string slotId)
    {
        return boardNumber + "|" + (slotId ?? string.Empty).Trim();
    }

    /// <summary>
    /// Costruisce lo slotId dall'indice (che ora parte da 0).
    /// Slot0 → "slot_00", Slot1 → "slot_01", ecc.
    /// </summary>
    private static string BuildSlotId(int index)
    {
        return "slot_" + Mathf.Max(0, index).ToString("00");
    }

    private static int MapBoardTier(LuckyShotBoardTier tier)
    {
        switch (tier)
        {
            case LuckyShotBoardTier.Board1: return 1;
            case LuckyShotBoardTier.Board2: return 2;
            case LuckyShotBoardTier.Board3: return 3;
            default: return 0;
        }
    }

    private int InferBoardNumberFromHierarchy(Transform t)
    {
        Transform current = t;
        while (current != null)
        {
            string n = current.name.ToLowerInvariant();
            if (n.Contains("board1")) return 1;
            if (n.Contains("board2")) return 2;
            if (n.Contains("board3")) return 3;
            current = current.parent;
        }

        return 0;
    }

    /// <summary>
    /// Estrae il numero dal nome dell'oggetto e lo restituisce come indice base-0.
    /// Slot0 → 0, Slot1 → 1, Slot6 → 6.
    /// Non fa più la sottrazione di 1 perché i nomi ora partono già da 0.
    /// </summary>
    private int InferSlotIndexFromName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return 0;

        string digits = string.Empty;
        for (int i = 0; i < objectName.Length; i++)
        {
            char c = objectName[i];
            if (char.IsDigit(c))
                digits += c;
        }

        if (string.IsNullOrEmpty(digits))
            return 0;

        if (!int.TryParse(digits, out int parsed))
            return 0;

        // I nomi partono da 0, quindi l'indice è direttamente il numero estratto.
        return Mathf.Max(0, parsed);
    }

    private Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase))
                return child;

            Transform deep = FindChildRecursive(child, childName);
            if (deep != null)
                return deep;
        }

        return null;
    }

    private string GetTransformName(Transform t)
    {
        return t == null ? "<null>" : t.name;
    }
}