using System;
using System.Collections.Generic;
using UnityEngine;

namespace KerbalMemorial
{
    // Preserve the stock dialog and its result data. Hide its canvas and block
    // interaction rather than dismissing it, deactivating its GameObject, or
    // invoking flight exit callbacks. No Harmony or other dependency required.
    internal sealed class FlightResultsGate
    {
        private readonly Dictionary<GameObject, CanvasGroup> held = new Dictionary<GameObject, CanvasGroup>();
        private Type resultsType;
        private bool lookedUp, failed;
        internal bool IsHolding
        {
            get
            {
                foreach (var g in held.Values)
                    if (g != null && g.gameObject.activeInHierarchy) return true;
                return false;
            }
        }

        internal void Hold()
        {
            if (failed) return;
            try
            {
                if (!lookedUp)
                {
                    lookedUp = true;
                    resultsType = typeof(FlightDriver).Assembly.GetType("KSP.UI.Dialogs.FlightResultsDialog");
                    if (resultsType == null || !typeof(Component).IsAssignableFrom(resultsType))
                        throw new InvalidOperationException("Stock FlightResultsDialog component not found.");
                }
                foreach (var obj in UnityEngine.Object.FindObjectsOfType(resultsType))
                {
                    var dialog = obj as Component;
                    if (dialog == null || !dialog.gameObject.activeInHierarchy) continue;
                    var behaviour = dialog as Behaviour;
                    if (behaviour != null && !behaviour.isActiveAndEnabled) continue;
                    Hide(dialog.gameObject);
                    // A nested group which ignores its parents can otherwise
                    // bypass the root's alpha and interaction restrictions.
                    foreach (var child in dialog.GetComponentsInChildren<CanvasGroup>(true))
                        if (child.ignoreParentGroups) Hide(child.gameObject);
                }
                // Enforce again after stock UI updates/animations.
                foreach (var group in held.Values) if (group != null) Suppress(group);
            }
            catch (Exception e)
            {
                // Fail open rather than strand the player without stock results.
                failed = true;
                Release();
                Debug.LogWarning("[KerbalMemorial] Cannot defer Flight Results: " + e.Message);
            }
        }

        private void Hide(GameObject target)
        {
            CanvasGroup group;
            if (!held.TryGetValue(target, out group) || group == null)
            {
                // Add a separate gate instead of overwriting the stock group's
                // alpha/interactivity. Stock animations and exit logic can
                // continue behind the invisible gate. Restore by removing only
                // the component we created, never a stock component.
                var existing = target.GetComponents<CanvasGroup>();
                group = target.AddComponent<CanvasGroup>();
                if (group == null || Array.IndexOf(existing, group) >= 0)
                    throw new InvalidOperationException("Could not attach an independent Flight Results canvas gate.");
                held[target] = group;
            }
            Suppress(group);
        }
        private static void Suppress(CanvasGroup group)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            group.ignoreParentGroups = false;
        }
        internal void Release()
        {
            foreach (var group in held.Values)
            {
                if (group == null) continue;
                // Destroy is deferred until end of frame. Make our gate neutral
                // immediately so the original dialog can render/interact now.
                group.alpha = 1f;
                group.interactable = true;
                group.blocksRaycasts = true;
                UnityEngine.Object.Destroy(group);
            }
            held.Clear();
        }
    }
}
