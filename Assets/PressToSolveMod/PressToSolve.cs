
using UnityEngine;

public class PressToSolve : MonoBehaviour {

	[SerializeField] KMBombModule module;
	[SerializeField] KMSelectable button;

	// Use this for initialization
	void Start () {
		button.OnInteract += delegate () { module.HandlePass(); return false; };
	}
}
