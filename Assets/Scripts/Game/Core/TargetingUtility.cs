using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public static class TargetingUtility
    {
        public static CharacterIdentity FindNearestEnemy(Vector3 fromPosition, Team myTeam)
        {
            var best = FindNearestEnemyInternal(fromPosition, myTeam, applyFocusFilter: true);
            if (best == null && myTeam == Team.Player && BattleCommandState.FocusFilter != FocusFireFilter.None)
            {
                // 狙い撃ち指定中の対象が全滅している等で0件の場合は、フィルタ無視で通常通り探す(行動が止まる事故を防ぐ保険)。
                best = FindNearestEnemyInternal(fromPosition, myTeam, applyFocusFilter: false);
            }
            return best;
        }

        static CharacterIdentity FindNearestEnemyInternal(Vector3 fromPosition, Team myTeam, bool applyFocusFilter)
        {
            CharacterIdentity best = null;
            float bestDistSqr = float.MaxValue;
            foreach (var c in CharacterRegistry.All)
            {
                if (c == null || c.Team == myTeam || !c.IsAlive) continue;
                if (applyFocusFilter && !PassesFocusFilter(c, myTeam)) continue;
                float d = (c.transform.position - fromPosition).sqrMagnitude;
                if (d < bestDistSqr)
                {
                    bestDistSqr = d;
                    best = c;
                }
            }
            return best;
        }

        public static CharacterIdentity FindRandomLivingEnemy(Team myTeam)
        {
            var candidates = new List<CharacterIdentity>();
            foreach (var c in CharacterRegistry.All)
            {
                if (c == null || c.Team == myTeam || !c.IsAlive) continue;
                if (!PassesFocusFilter(c, myTeam)) continue;
                candidates.Add(c);
            }
            if (candidates.Count == 0 && myTeam == Team.Player && BattleCommandState.FocusFilter != FocusFireFilter.None)
            {
                foreach (var c in CharacterRegistry.All)
                {
                    if (c == null || c.Team == myTeam || !c.IsAlive) continue;
                    candidates.Add(c);
                }
            }
            if (candidates.Count == 0) return null;
            return candidates[Random.Range(0, candidates.Count)];
        }

        // 狙い撃ちコマンド(ボス集中攻撃/ボス以外集中攻撃)は、プレイヤー側キャラの狙い先選定にのみ影響する
        // (ボス・召喚敵側のプレイヤーへの狙い先選定は常に通常通り)。
        static bool PassesFocusFilter(CharacterIdentity candidate, Team myTeam)
        {
            if (myTeam != Team.Player) return true;
            switch (BattleCommandState.FocusFilter)
            {
                case FocusFireFilter.BossOnly: return candidate.IsBoss;
                case FocusFireFilter.ExcludeBoss: return !candidate.IsBoss;
                default: return true;
            }
        }

        public static int CountAlliesInRange(Vector3 fromPosition, Team myTeam, float range, CharacterIdentity self)
        {
            int count = 0;
            float rangeSqr = range * range;
            foreach (var c in CharacterRegistry.All)
            {
                if (c == null || c == self || c.Team != myTeam || !c.IsAlive) continue;
                if ((c.transform.position - fromPosition).sqrMagnitude <= rangeSqr) count++;
            }
            return count;
        }

        // 感知距離コライダーを子オブジェクトとして生成する (シーン上での可視化・将来の拡張用。判定自体は距離計算で行う)。
        public static SphereCollider CreateRangeGizmoCollider(Transform parent, string name, float radius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = radius;
            return col;
        }

        // 感知距離・維持距離などをエディタ上でのみ可視化するためのワイヤー円 (OnDrawGizmosSelectedから呼ぶ想定)。
        public static void DrawGizmoCircle(Vector3 center, float radius, int segments = 48)
        {
            if (radius <= 0f) return;
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
