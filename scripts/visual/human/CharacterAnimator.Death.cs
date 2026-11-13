using Godot;
using static AnimationKit;
using System.Collections.Generic;

/// <summary>
/// Morrer e renascer: o clipe de morte, o corpo no chão, o desbotar e a
/// volta ao normal.
/// </summary>
public partial class CharacterAnimator
{
	/// <summary>
	/// Quanto o corpo desce ao morrer, para encostar no chão.
	///
	/// De pé o modelo casa com a cápsula, porque os dois têm o pé na
	/// base. Deitado não: a pose final do clipe de morte deixa o corpo
	/// um pouco acima do plano de onde ele foi animado, e o que sobra
	/// aparece como um defunto pairando. Um número resolve, e é melhor
	/// como número do que como conserto na animação — clipe de morte
	/// novo traz a própria folga.
	/// </summary>
	[ExportGroup("Corpo morto")]
	[Export] public float CorpseDrop = 0.12f;

	/// <summary>
	/// Tempo que o corpo fica à mostra no chão antes de sumir.
	///
	/// Contado a partir do fim do clipe de morte, não da morte: o
	/// clipe tem três segundos e o corpo passa boa parte deles ainda
	/// caindo. Contar do começo fazia o corpo desbotar no meio da
	/// queda.
	/// </summary>
	[Export] public float CorpseHold = 1.2f;

	/// <summary>Duração do desaparecimento.</summary>
	[Export] public float CorpseFade = 1.2f;

	/// <summary>
	/// Quanto o corpo afunda enquanto some. Zero: some no lugar.
	///
	/// Afundar esconde melhor o instante da saída, mas só funciona
	/// quando o corpo está sobre piso — em cima de uma caixa ou de um
	/// telhado ele atravessa antes de apagar, e aí o truque aparece.
	/// Fica como número para quem quiser.
	/// </summary>
	[Export] public float CorpseSink = 0f;

	private bool _dead;
	private double _corpseTimer;
	private double _deathLength;
	private readonly List<BaseMaterial3D> _fadeMaterials = new();
	private bool _hasDeath;

	private void OnDied()
	{
		StopReload();
		_dead = true;
		_corpseTimer = 0;

		if (_hasDeath) _playback?.Travel("Death");
		else if (Debug) GD.Print("CharacterAnimator: sem clipe de morte.");
	}

	/// <summary>
	/// O corpo fica um instante, depois afunda e some.
	///
	/// A pausa antes é o que faz a morte ser lida: sumir no mesmo
	/// quadro em que cai tira do jogador a chance de ver o que
	/// aconteceu, e quem matou perde a confirmação do abate.
	///
	/// Afundar junto com a opacidade esconde o instante da saída. Só
	/// apagar deixa o corpo piscar para fora, e isso o olho pega.
	/// </summary>
	private void UpdateCorpse(double delta)
	{
		_corpseTimer += delta;

		// Nada acontece enquanto o corpo cai. Só depois de pousado é
		// que começa a contar o tempo em que ele fica à mostra.
		double grounded = _corpseTimer - _deathLength;
		if (grounded < 0) return;

		float settle = Mathf.Clamp((float)grounded / 0.25f, 0f, 1f);

		double t = (grounded - CorpseHold) / Mathf.Max(CorpseFade, 0.01f);
		float progress = t <= 0 ? 0f : Mathf.Clamp((float)t, 0f, 1f);

		// Só o afundar da morte. A altura dos pés é do nó de cima.
		_model.Position = new Vector3(
			_model.Position.X,
			-CorpseDrop * settle - CorpseSink * progress,
			_model.Position.Z);

		if (progress <= 0) return;

		if (_fadeMaterials.Count == 0) PrepareFade();

		foreach (BaseMaterial3D material in _fadeMaterials)
		{
			Color color = material.AlbedoColor;
			color.A = 1f - progress;
			material.AlbedoColor = color;
		}

		if (progress >= 1f) _model.Visible = false;
	}

	/// <summary>
	/// Prepara os materiais para receber opacidade.
	///
	/// Duplicar é obrigatório: o material vem da importação e é
	/// compartilhado por todos os bots da cena. Sem a cópia, matar um
	/// faz os outros desbotarem junto — e esse é o tipo de defeito que
	/// se procura no código de morte, que está certo, em vez de no
	/// material, que é onde está.
	/// </summary>
	private void PrepareFade()
	{
		Collect(_model);

		void Collect(Node node)
		{
			if (node is MeshInstance3D mesh && mesh.Mesh != null)
			{
				for (int surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
				{
					if (mesh.GetActiveMaterial(surface) is not BaseMaterial3D source)
						continue;

					var copy = (BaseMaterial3D)source.Duplicate();
					copy.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;

					mesh.SetSurfaceOverrideMaterial(surface, copy);
					_fadeMaterials.Add(copy);
				}
			}

			foreach (Node child in node.GetChildren()) Collect(child);
		}
	}

	private void OnRespawned()
	{
		_dead = false;
		_corpseTimer = 0;
		_blend = Vector2.Zero;
		_crouched = false;
		_airborne = false;

		// Volta tudo: um corpo que renasce meio transparente e meio
		// enterrado é o mesmo defeito, só adiado.
		foreach (BaseMaterial3D material in _fadeMaterials)
		{
			Color color = material.AlbedoColor;
			color.A = 1f;
			material.AlbedoColor = color;
		}

		_model.Position = new Vector3(_model.Position.X, 0, _model.Position.Z);
		_model.Visible = true;

		_playback?.Start("Locomotion");
	}
}
