using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// OUTIL DE DIAGNOSTIC — à supprimer une fois la plaque validée.
///
/// Il se lance TOUT SEUL au Play (rien à mettre dans la scène) et répond à trois questions :
///
///   1. Quels périphériques Unity voit-il ?
///   2. Chaque action (LeftFlipper, RightFlipper, LaunchBall, Quit) trouve-t-elle vraiment
///      un bouton de la plaque ? Si un chemin de binding est faux, l'action n'a AUCUN contrôle
///      résolu et c'est écrit en rouge.
///   3. Quand tu appuies sur un bouton : quel contrôle c'est, et quelle action se déclenche.
///
/// Si "BOUTON PRESSÉ" s'affiche sans "ACTION DÉCLENCHÉE" juste après, le problème est dans
/// PinballControls.cs (le chemin du binding). Si l'action se déclenche mais que le flipper ne
/// bouge pas, le problème est côté flipper (Side, HingeJoint).
/// </summary>
public class BoardButtonProbe : MonoBehaviour
{
    // Passe à false (ou supprime ce fichier) quand c'est validé.
    private static bool autoStart = true;

    private PinballControls board;
    private InputAction[] actions;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!autoStart) return;

        GameObject probe = new GameObject("[BoardButtonProbe]");
        DontDestroyOnLoad(probe);
        probe.AddComponent<BoardButtonProbe>();
    }

    private void OnEnable()
    {
        InputSystem.onDeviceChange += HandleDeviceChange;

        board = new PinballControls();
        actions = new[]
        {
            board.GamePlayPF.LeftFlipper,
            board.GamePlayPF.RightFlipper,
            board.GamePlayPF.LaunchBall,
            board.GamePlayPF.Quit
        };

        board.GamePlayPF.Enable();
    }

    private void OnDisable()
    {
        InputSystem.onDeviceChange -= HandleDeviceChange;

        if (board == null) return;

        board.GamePlayPF.Disable();
        board.Dispose();
        board = null;
    }

    private void Start()
    {
        Debug.Log($"[Plaque] {InputSystem.devices.Count} périphérique(s) détecté(s) :");
        foreach (InputDevice device in InputSystem.devices)
        {
            Debug.Log($"[Plaque]   \"{device.displayName}\"  layout={device.layout}  path={device.path}");
        }

        ReportBindings();

        Debug.Log("[Plaque] Appuie maintenant sur les boutons de la plaque, un par un.");
    }

    /// <summary>Vérifie que chaque action trouve bien un contrôle de la plaque.</summary>
    private void ReportBindings()
    {
        Debug.Log("[Plaque] --- Bindings résolus ---");

        foreach (InputAction action in actions)
        {
            if (action.controls.Count == 0)
            {
                Debug.LogWarning(
                    $"[Plaque] Action '{action.name}' : AUCUN bouton trouvé. " +
                    "Soit le chemin du binding ne correspond à rien dans PinballControls.cs, " +
                    "soit la plaque n'était pas branchée quand le Play a démarré.", this);
                continue;
            }

            foreach (InputControl control in action.controls)
            {
                Debug.Log($"[Plaque] Action '{action.name}'  ->  {control.path}");
            }
        }
    }

    private void Update()
    {
        // 1) Quels boutons physiques sont pressés ?
        foreach (InputDevice device in InputSystem.devices)
        {
            // On ignore clavier / souris : ils pollueraient le relevé
            if (device is Keyboard || device is Mouse || device is Pen || device is Touchscreen) continue;

            foreach (InputControl control in device.allControls)
            {
                if (control is ButtonControl button && button.wasPressedThisFrame && !button.synthetic)
                {
                    Debug.Log($"[Plaque] BOUTON PRESSÉ -> {button.path}");
                }
            }
        }

        // 2) Quelles actions se déclenchent ?
        if (actions == null) return;

        foreach (InputAction action in actions)
        {
            if (action.WasPerformedThisFrame())
            {
                Debug.Log($"[Plaque] ACTION DÉCLENCHÉE -> {action.name}");
            }
        }
    }

    private void HandleDeviceChange(InputDevice device, InputDeviceChange change)
    {
        // On ne journalise que les vraies arrivées / départs : les Reset, Enabled et Disabled
        // déclenchés au lancement et à l'arrêt du Play ne feraient que polluer la Console.
        if (change != InputDeviceChange.Added && change != InputDeviceChange.Removed) return;

        Debug.Log($"[Plaque] Périphérique '{change}' : \"{device.displayName}\" (layout = {device.layout})");
    }
}
