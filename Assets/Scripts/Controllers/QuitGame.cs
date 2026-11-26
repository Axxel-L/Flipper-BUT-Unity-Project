using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Bouton Quit (Xin-Mo button2) : termine la partie en cours.
/// Le game over est déclenché, le score est enregistré et la modale REJOUER s'affiche.
/// </summary>
public class QuitGame : MonoBehaviour
{
    private PinballControls controls;
    private InputAction quitAction;

    void Awake()
    {
        controls = new PinballControls();
        quitAction = controls.GamePlayPF.Quit;
    }

    void OnEnable()
    {
        if (quitAction != null)
        {
            quitAction.Enable();
        }
    }

    void OnDisable()
    {
        if (quitAction != null)
        {
            quitAction.Disable();
        }
    }

    void OnDestroy()
    {
        if (controls != null)
        {
            controls.Dispose();
        }
    }

    void Update()
    {
        if (quitAction != null && quitAction.WasPressedThisFrame())
        {
            EndCurrentGame();
        }
    }

    void EndCurrentGame()
    {
        PinballGameManager manager = PinballGameManager.Instance;
        if (manager != null)
        {
            manager.EndGame();
            return;
        }

        // Secours : pas de gestionnaire de partie (scène de menu / test isolé).
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
