using UnityEngine;

/// <summary>
/// Contrôle un flipper 3D avec un HingeJoint.
/// Le flipper tourne autour de l'axe Y.
/// 
/// À utiliser séparément sur le flipper gauche et le flipper droit.
/// 
/// Prérequis :
/// - Rigidbody
/// - HingeJoint
/// </summary>
[RequireComponent(typeof(HingeJoint))]
[RequireComponent(typeof(Rigidbody))]
public class FlipperController : MonoBehaviour
{
    [Header("Configuration de la touche")]
    [Tooltip("Touche associée à ce flipper")]
    public KeyCode toucheActivation = KeyCode.RightArrow;

    [Header("Mouvement")]
    [Tooltip("Vitesse de rotation quand le flipper est activé")]
    public float vitesseActivee = 720f;

    [Tooltip("Vitesse de rotation quand le flipper revient au repos")]
    public float vitesseRepos = -720f;

    [Tooltip("Couple maximum du moteur")]
    public float coupleMax = 10000f;

    private HingeJoint hinge;
    private JointMotor moteur;

    void Awake()
    {
        hinge = GetComponent<HingeJoint>();

        // Active le moteur du HingeJoint
        hinge.useMotor = true;

        // Récupère le moteur
        moteur = hinge.motor;

        // Configure le couple maximum
        moteur.force = coupleMax;

        // Vitesse initiale : retour vers la position de repos
        moteur.targetVelocity = vitesseRepos;

        // Applique les réglages
        hinge.motor = moteur;
    }

    void Update()
    {
        // Appui sur la touche
        if (Input.GetKeyDown(toucheActivation))
        {
            ActiverFlipper();
        }

        // Relâchement de la touche
        if (Input.GetKeyUp(toucheActivation))
        {
            RelacherFlipper();
        }
    }

    private void ActiverFlipper()
    {
        moteur.targetVelocity = vitesseActivee;
        hinge.motor = moteur;
    }

    private void RelacherFlipper()
    {
        moteur.targetVelocity = vitesseRepos;
        hinge.motor = moteur;
    }
}