namespace Game
{
    // 「今、意図的に敵との距離を保って待機している」ことをCharacterChargeAssistへ伝えるためのマーカー。
    // StealthKite/Firework/ChainLightningのような遠距離維持型アビリティが実装する。
    // これが無いと、うまく距離を保てている状態を「膠着している(スタックしている)」と誤認したまま
    // CharacterChargeAssistが強制的に敵へ突進させてしまい、せっかく保った距離を自ら崩して
    // 近づいては離れてを繰り返す(振動する)原因になる。
    public interface IDistanceHoldingAbility
    {
        bool IsHoldingDistance { get; }
    }
}
