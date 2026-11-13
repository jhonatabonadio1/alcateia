using Godot;

/// <summary>Busca na árvore de nós.</summary>
public static class Nodes
{
	/// <summary>O primeiro nó do tipo pedido, em profundidade, começando
	/// pelo próprio <paramref name="root"/>; <c>null</c> se não houver.</summary>
	public static T FindFirst<T>(Node root) where T : Node
	{
		if (root == null) return null;
		if (root is T found) return found;

		foreach (Node child in root.GetChildren())
		{
			T inner = FindFirst<T>(child);
			if (inner != null) return inner;
		}

		return null;
	}
}
