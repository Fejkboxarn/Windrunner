using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AirflowPrototype
{
    /// <summary>
    /// Safety net for runtime-created Input System actions.
    ///
    /// PlayerInputReader creates its InputActionMap/actions at runtime. After
    /// script recompiles or unusual Enter Play Mode configurations, those
    /// objects can occasionally exist in a disabled state on the first run.
    ///
    /// This component waits until PlayerInputReader has initialized, then
    /// explicitly enables every InputActionMap / InputAction field it owns.
    /// It does not create bindings or change any input values.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class PlayerInputStartupRepair : MonoBehaviour
    {
        [SerializeField]
        private PlayerInputReader inputReader;

        [Tooltip(
            "Also checks again on the following frame. This covers initialization " +
            "that happens during Start rather than Awake/OnEnable.")]
        [SerializeField]
        private bool verifyNextFrame = true;

        [Header("Debug")]
        [SerializeField]
        private bool logRepairs;

        private bool _verifiedNextFrame;

        private void Awake()
        {
            ResolveReader();
        }

        private void Start()
        {
            RepairInputState();
        }

        private void LateUpdate()
        {
            if (!verifyNextFrame ||
                _verifiedNextFrame)
            {
                return;
            }

            _verifiedNextFrame = true;
            RepairInputState();
        }

        public void Configure(
            PlayerInputReader newInputReader)
        {
            inputReader = newInputReader;
        }

        [ContextMenu("Repair Input State")]
        public void RepairInputState()
        {
            ResolveReader();

            if (inputReader == null)
                return;

            BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            FieldInfo[] fields =
                inputReader
                    .GetType()
                    .GetFields(flags);

            int mapsEnabled = 0;
            int actionsEnabled = 0;

            for (int i = 0;
                 i < fields.Length;
                 i++)
            {
                FieldInfo field =
                    fields[i];

                object value =
                    field.GetValue(
                        inputReader);

                if (value is InputActionMap map)
                {
                    if (!map.enabled)
                    {
                        map.Enable();
                        mapsEnabled++;
                    }

                    continue;
                }

                if (value is InputAction action)
                {
                    if (!action.enabled)
                    {
                        action.Enable();
                        actionsEnabled++;
                    }
                }
            }

            if (logRepairs &&
                (mapsEnabled > 0 ||
                 actionsEnabled > 0))
            {
                Debug.Log(
                    $"PlayerInputStartupRepair enabled " +
                    $"{mapsEnabled} action map(s) and " +
                    $"{actionsEnabled} action(s).",
                    this);
            }
        }

        private void ResolveReader()
        {
            if (inputReader == null)
            {
                inputReader =
                    GetComponent<PlayerInputReader>();
            }

            if (inputReader == null)
            {
                inputReader =
                    FindAnyObjectByType<PlayerInputReader>();
            }
        }
    }
}
