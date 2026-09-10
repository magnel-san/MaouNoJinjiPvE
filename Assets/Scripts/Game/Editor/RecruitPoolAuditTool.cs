using System.Linq;
using System.Text;
using Game.Flow;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    // RecruitPool.Options内の各CharacterRecruitOptionのRank(金/銀/銅)・Role(アタッカー/タンク/ヒーラー)を
    // 集計してログ表示する。目標比率(金6:銀15:銅9 / アタッカー15:タンク9:ヒーラー6)との差分確認用。
    // BossAnimatorSetup.csと同じ[MenuItem]パターン。
    internal static class RecruitPoolAuditTool
    {
        [MenuItem("Game/Flow/Audit Recruit Pool Ranks-Roles")]
        static void Audit()
        {
            var pool = ResolveSelectedOrFirstPool();
            if (pool == null)
            {
                Debug.LogWarning("[RecruitPoolAuditTool] Project WindowでRecruitPoolアセットを選択してから実行してください。");
                return;
            }

            var options = (pool.Options ?? new CharacterRecruitOption[0]).Where(o => o != null).ToList();
            var total = options.Count;

            var sb = new StringBuilder();
            sb.AppendLine($"[RecruitPoolAuditTool] '{pool.name}' 集計 (全{total}体)");

            sb.AppendLine("--- Rank(目標: 金6/銀15/銅9) ---");
            foreach (CharacterRank rank in System.Enum.GetValues(typeof(CharacterRank)))
            {
                var count = options.Count(o => o.Rank == rank);
                sb.AppendLine($"  {rank}: {count}体");
            }

            sb.AppendLine("--- Role(目標: アタッカー:タンク:ヒーラー = 5:3:2) ---");
            foreach (CharacterRole role in System.Enum.GetValues(typeof(CharacterRole)))
            {
                var count = options.Count(o => o.Role == role);
                sb.AppendLine($"  {role}: {count}体");
            }

            var untagged = options.Where(o => o.CharacterPrefab == null).Select(o => o.DisplayName).ToList();
            if (untagged.Count > 0)
            {
                sb.AppendLine($"--- プレファブ未設定 ({untagged.Count}体) ---");
                sb.AppendLine("  " + string.Join(", ", untagged));
            }

            Debug.Log(sb.ToString());
        }

        // HealSkillを持つプレファブのRoleを一括でHealerに初期設定する(それ以外は触らない)。
        // ランク・アタッカー/タンクの割り振りは引き続き手動でInspectorから行う。
        [MenuItem("Game/Flow/Auto-fill Healer Role From Ability")]
        static void AutoFillHealerRole()
        {
            var pool = ResolveSelectedOrFirstPool();
            if (pool == null)
            {
                Debug.LogWarning("[RecruitPoolAuditTool] Project WindowでRecruitPoolアセットを選択してから実行してください。");
                return;
            }

            var updated = 0;
            foreach (var option in pool.Options ?? new CharacterRecruitOption[0])
            {
                if (option == null || option.CharacterPrefab == null) continue;
                if (option.Role == CharacterRole.Healer) continue;
                if (option.CharacterPrefab.GetComponent<HealSkill>() == null) continue;

                option.Role = CharacterRole.Healer;
                EditorUtility.SetDirty(option);
                updated++;
            }

            if (updated > 0) AssetDatabase.SaveAssets();
            Debug.Log($"[RecruitPoolAuditTool] HealSkill所持キャラ {updated}体のRoleをHealerに設定しました。");
        }

        static RecruitPool ResolveSelectedOrFirstPool()
        {
            if (Selection.activeObject is RecruitPool fromSelection) return fromSelection;

            var guids = AssetDatabase.FindAssets("t:Game.Flow.RecruitPool");
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<RecruitPool>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
