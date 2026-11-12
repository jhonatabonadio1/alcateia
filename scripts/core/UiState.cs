using Godot;

/// <summary>
/// Quantos menus estão abertos.
///
/// Existe porque dois menus independentes brigavam pelo mesmo recurso
/// global: cada um capturava ou liberava o mouse ao fechar, sem saber
/// do outro. Fechar o menu de dificuldade capturava o cursor com a
/// tela de lotes ainda aberta.
///
/// Com um contador, o cursor só é recapturado quando o último menu
/// fecha. É a mesma ideia de um semáforo: o recurso é liberado pelo
/// último que solta, não pelo primeiro.
/// </summary>
public static class UiState
{
    public static int Open { get; private set; }

    public static bool AnyOpen => Open > 0;

    /// <summary>Verdadeiro quando só um menu está aberto — usado por
    /// um menu para saber se ele está no topo e pode contar o tempo.</summary>
    public static bool IsAlone => Open == 1;

    public static void Push()
    {
        Open++;
        Apply();
    }

    public static void Pop()
    {
        Open = Mathf.Max(Open - 1, 0);
        Apply();
    }

    private static void Apply()
    {
        Input.MouseMode = AnyOpen
            ? Input.MouseModeEnum.Visible
            : Input.MouseModeEnum.Captured;
    }
}
