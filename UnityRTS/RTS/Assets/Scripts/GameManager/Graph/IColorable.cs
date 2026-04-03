using UnityEngine;

namespace GameManager.Graph
{
	/// <summary>
	/// Marks a graph node element as visually tintable.
	/// Used for debug visualization — e.g., highlighting searched cells
	/// or coloring path results during development.
	/// </summary>
	internal interface IColorable
    {
	    /// <summary>Apply a color tint to this element's visual representation.</summary>
	    void ChangeColor(Color color);
    }
}
