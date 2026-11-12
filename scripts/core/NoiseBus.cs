using Godot;
using System;
using System.Collections.Generic;

/// <summary>Tipo de ruído. Serve para o ouvinte decidir o quanto
/// aquilo é interessante, não só o quanto é alto.</summary>
public enum NoiseKind
{
    Step,
    Land,
    Shot,
    Howl,
    Explosion
}

public readonly struct Noise
{
    public readonly Vector3 Position;

    /// <summary>Raio em metros no qual o som ainda é audível.</summary>
    public readonly float Radius;

    public readonly NoiseKind Kind;
    public readonly Node Source;

    public Noise(Vector3 position, float radius, NoiseKind kind, Node source)
    {
        Position = position;
        Radius = radius;
        Kind = kind;
        Source = source;
    }
}

/// <summary>
/// Barramento de ruído: quem faz barulho publica, quem ouve se
/// inscreve. Nenhum dos dois conhece o outro.
///
/// É o padrão de "stimulus broadcast" usado em IA de stealth desde os
/// anos 2000. A alternativa — cada bot varrer o mundo procurando
/// coisas barulhentas — acopla a IA a tudo que existe e não escala.
///
/// Som aqui é informação de jogo, não áudio. Quando houver som de
/// verdade, ele acompanha o evento em vez de substituí-lo: o que o bot
/// ouve e o que o jogador ouve precisam ser a mesma coisa, senão o
/// jogador não consegue prever a reação.
/// </summary>
public static class NoiseBus
{
    private static readonly List<Action<Noise>> Listeners = new();

    public static void Subscribe(Action<Noise> listener)
    {
        if (!Listeners.Contains(listener))
            Listeners.Add(listener);
    }

    public static void Unsubscribe(Action<Noise> listener)
    {
        Listeners.Remove(listener);
    }

    public static void Emit(Noise noise)
    {
        // Cópia: um ouvinte pode se desinscrever ao reagir.
        foreach (Action<Noise> listener in Listeners.ToArray())
            listener(noise);
    }

    public static void Emit(Vector3 position, float radius, NoiseKind kind, Node source)
        => Emit(new Noise(position, radius, kind, source));
}
