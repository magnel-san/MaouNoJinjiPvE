using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.EditorTools
{
    // ボス/最終決戦の勇者用アニメーション(登場・専用攻撃・吹き飛ばし・移動)を、既存のAnimator
    // Controllerへワンクリックで追加配線するツール。GameBalanceConfigBootstrap.csと同じ
    // [MenuItem]パターン。トリガーパラメータ+Any State遷移する空のStateを自動生成するので、
    // ユーザーは各Stateの「Motion」スロットへ用意したアニメーションクリップをドラッグするだけでよい。
    // Project WindowでAnimatorControllerアセットを選択するか、Animatorコンポーネントを持つ
    // GameObject(プレハブ含む)を選択した状態で実行する。
    internal static class BossAnimatorSetup
    {
        const string TriggerSpawn = "Spawn";
        const string TriggerSpecialAttack = "SpecialAttack";
        const string TriggerKnockback = "Knockback";
        const string FloatMoveSpeed = "MoveSpeed";
        const string BoolIsMoving = "IsMoving";

        [MenuItem("Game/Boss/Setup Boss Animator Parameters")]
        static void SetupSelected()
        {
            var controller = ResolveSelectedController();
            if (controller == null)
            {
                Debug.LogWarning("[BossAnimatorSetup] Project WindowでAnimatorControllerアセットを選択するか、" +
                    "Animatorコンポーネントを持つGameObject/プレハブを選択してから実行してください。");
                return;
            }

            AddParameterIfMissing(controller, TriggerSpawn, AnimatorControllerParameterType.Trigger);
            AddParameterIfMissing(controller, TriggerSpecialAttack, AnimatorControllerParameterType.Trigger);
            AddParameterIfMissing(controller, TriggerKnockback, AnimatorControllerParameterType.Trigger);
            AddParameterIfMissing(controller, FloatMoveSpeed, AnimatorControllerParameterType.Float);
            AddParameterIfMissing(controller, BoolIsMoving, AnimatorControllerParameterType.Bool);

            var stateMachine = controller.layers[0].stateMachine;
            var defaultState = stateMachine.defaultState;

            AddTriggerStateIfMissing(stateMachine, defaultState, TriggerSpawn);
            AddTriggerStateIfMissing(stateMachine, defaultState, TriggerSpecialAttack);
            AddTriggerStateIfMissing(stateMachine, defaultState, TriggerKnockback);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log($"[BossAnimatorSetup] '{controller.name}' へパラメータ・State を追加しました。" +
                "各Stateの Motion スロットへアニメーションクリップをドラッグしてください" +
                "(移動(MoveSpeed/IsMoving)は既存のIdle/Move State側で自由に使ってください)。");
        }

        static AnimatorController ResolveSelectedController()
        {
            if (Selection.activeObject is AnimatorController fromAsset) return fromAsset;

            if (Selection.activeGameObject != null)
            {
                var animator = Selection.activeGameObject.GetComponentInChildren<Animator>();
                if (animator != null && animator.runtimeAnimatorController is AnimatorController fromGameObject)
                {
                    return fromGameObject;
                }
            }

            return null;
        }

        static void AddParameterIfMissing(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            foreach (var p in controller.parameters)
            {
                if (p.name == name) return;
            }
            controller.AddParameter(name, type);
        }

        static void AddTriggerStateIfMissing(AnimatorStateMachine stateMachine, AnimatorState returnState, string triggerName)
        {
            foreach (var s in stateMachine.states)
            {
                if (s.state.name == triggerName) return;
            }

            var state = stateMachine.AddState(triggerName);

            var enterTransition = stateMachine.AddAnyStateTransition(state);
            enterTransition.hasExitTime = false;
            enterTransition.duration = 0.1f;
            enterTransition.AddCondition(AnimatorConditionMode.If, 0f, triggerName);

            if (returnState != null)
            {
                var exitTransition = state.AddTransition(returnState);
                exitTransition.hasExitTime = true;
                exitTransition.exitTime = 0.9f;
                exitTransition.hasFixedDuration = true;
                exitTransition.duration = 0.15f;
            }
        }
    }
}
