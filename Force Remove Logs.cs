using SonsSdk;
using Sons.Construction;
using Construction;
using RedLoader;
using Construction.Utils;
using Sons.Crafting.Structures;
using UnityEngine;
using HarmonyLib;
using Sons;
using Sons.Input;
using System.Runtime.CompilerServices;
using Sons.Gui;
using Construction.Multiplayer;
using UnityEngine.Profiling;
using Sons.Gameplay.Grabber;
using TheForest.Utils;
using Construction.Anim;
using Bolt;
using UdpKit;
using SonsSdk.Networking;
using Sons.Multiplayer;
using System.Drawing;
using RedLoader.Utils;
using System.Diagnostics.Tracing;
using Microsoft.VisualBasic;
using static SteamSocket;

namespace Force_Remove_Logs;

public class Force_Remove_Logs : SonsMod
{
    public static string _modVersion;
    public static bool forceDisableMod = true;
    public static string disableReason;

    private bool startedDismantling = false;
    private bool startedAnimation = false;
    private bool buildModeState = false;
    private float dismantleTimer = 0;
    private float forceRemovalTime = 1;

    private static Structure _extractedTargetStructure;
    private static StructureElement _extractedTargetElement;
    private static bool isNonRemovable = false;
    private static ConstructionManager _extractedConstructionManager;
    private static ModuleState _extractedState;
    private static GenericGrabberTargetProvider _extractedGenericGrabberTargetProvider;

    public Force_Remove_Logs()
    {
        // Uncomment any of these if you need a method to run on a specific update loop.
        OnUpdateCallback = OnUpdate;
        //OnLateUpdateCallback = MyLateUpdateMethod;
        //OnFixedUpdateCallback = MyFixedUpdateMethod;
        //OnGUICallback = MyGUIMethod;

        // Uncomment this to automatically apply harmony patches in your assembly.
        HarmonyPatchAll = true;
    }

    protected override void OnInitializeMod()
    {
        // Do your early mod initialization which doesn't involve game or sdk references here
        Config.Init();
        NetworkManager.RegisterPackets();
        _modVersion = Manifest.Version;

        SdkEvents.OnWorldExited.Subscribe(OnWorldExitedCallback);
    }

    protected override void OnSdkInitialized()
    {
        // Do your mod initialization which involves game or sdk references here
        // This is for stuff like UI creation, event registration etc.
        Force_Remove_LogsUi.Create();

        // Add in-game settings ui for your mod.
        // SettingsRegistry.CreateSettings(this, null, typeof(Config));
    }

    void OnWorldExitedCallback()
    {
        RLog.Msg("Returned to main menu... locking down mod...");
        forceDisableMod = true;
    }

    protected override void OnGameStart()
    {
        // This is called once the player spawns in the world and gains control.
        if (BoltNetwork.isServerOrNotRunning)
        {
            forceDisableMod = false;
            RLog.Msg("Enabled mod due to host or Singleplayer");
            //Initialize event handler
            EventHandler.Create();
            return;
        }

        if (forceDisableMod)
        {
            DelayedExecution().RunCoro();
        }
    }

    private static System.Collections.IEnumerator DelayedExecution()
    { 
        yield return new WaitForSeconds(10f);

        switch (disableReason)
        {
            case "mismatchVersion":
                SonsTools.ShowMessage($"Force Remove Logs has been disabled due to mismatching mod version!", 30f);
                break;
            default:
                SonsTools.ShowMessage($"Force Remove Logs has been disabled, mod not installed on server/host!", 30f);
                break;
        }
    }

    private void OnUpdate()
    {

        if (forceDisableMod)
        {
            return;
        }

        if(_extractedGenericGrabberTargetProvider == null || _extractedState == null)
        {
            stopAnimation();
            return;
        }

        //DismantleHeld is released after the prompt dissapears i want to continue checking for if the button is kept held afterwards
        if (ConstructionInput.DismantleHeld()) //True as long as dismantling | Check for non removable | check if target is set
        {
            startedDismantling = true;
        }
        

        //bool dismantleButtonDown = Sons.Input.InputSystem.GetButtonDown(ConstructionInput.DismantleElement);
        bool dismantleButtonUp = Sons.Input.InputSystem.GetButtonUp(ConstructionInput.DismantleElement);

        if (dismantleButtonUp)
        {
            startedDismantling = false;
            dismantleTimer = 0;
            stopAnimation();
            return;
        }

        if (startedDismantling)
        {
            //Check if it cannot be normally remove and we already have a target structure
            if (isNonRemovable && _extractedTargetStructure != null)
            {
                SetBuildMode(true);
                RLog.Msg("Adding time... " + dismantleTimer + "  " + _extractedTargetStructure.gameObject.name);
                dismantleTimer += Time.deltaTime;
                // after half a second start the dismantle animation
                if (dismantleTimer > 0.5f)
                {
                    if (!startedAnimation) 
                    {
                        initAnimation(); 
                    }
                }

                //Animation done after ~ 0.5, remove object
                if (dismantleTimer > forceRemovalTime)
                {
                    RLog.Msg("FORCE REMOVING TARGET OBJECT!!!");
                    dismantleTimer = 0;
                    startedDismantling = false;
                    stopAnimation();
                    GameObject.Destroy(GameObject.Find(_extractedTargetStructure.gameObject.name));
                    addItems();
                    //Run networking here
                    NetworkManager.SendStringMessage(_extractedTargetStructure.gameObject.name);
                }
            }
        } else {
            stopAnimation();
        }
    }

    private void initAnimation()
    {
        startedAnimation = true;

        if (_extractedTargetElement.transform.childCount > 0)
        {
            ClipTypes dismantleAnim = _extractedTargetElement.Profile.DismantleAnim;
            _extractedConstructionManager.Previews.PlayPreviewAnimation(dismantleAnim, TweenTypes.Loop, _extractedTargetElement.transform);
        }

        if (_extractedState.ActiveProfile.PlaceAnimBeganAudioClip)
        {
            _extractedState.ActiveProfile.PlaceAnimBeganAudioClip.Play(_extractedTargetElement.transform, false);
        }
    }

    private void stopAnimation()
    {
        if (buildModeState)
        {
            SetBuildMode(false);
        }

        if (startedAnimation)
        {
            _extractedConstructionManager.Previews.StopPreviewAnimation();
            startedAnimation = false;
        }
    }

    public void SetBuildMode(bool enable)
    {
        if (enable)
        {
            LocalPlayer.FpCharacter.EnableMovementSlow(0.4f);
            buildModeState = true;
        }
        else
        {
            buildModeState = false;
            LocalPlayer.FpCharacter.DisableMovementSlow();
            LocalPlayer.FpCharacter.MovementLocked = false;
        }

        LocalPlayer.MainRotator.rotationSpeed = enable ? 0.1f : 5f;
        LocalPlayer.CamRotator.rotationSpeed = enable ? 0.1f : 5f;
        LocalPlayer.FpCharacter.SetCanJump(!enable);
        LocalPlayer.FpCharacter.BlockCrouch = enable;
    }

    private void addItems()
    {
        ElementProfile elementProfile = _extractedTargetElement.Profile;
        uint disassembleItemYield = elementProfile.DisassembleItemYield;

        //If it has no instance return out and add no item, for example visual only structures
        if (elementProfile.DisassembleItemInstance?._itemID == null) { return; }
        int num2 = 0;
        while ((long)num2 < (long)((ulong)disassembleItemYield))
        {
            _extractedConstructionManager.Items.AddOrDrop(elementProfile.Item, elementProfile.DisassembleItemInstance);
            num2++;
        }
    }


    [HarmonyPatch(typeof(CollectPlacedElementModule), "TryStage")]
    private static class _AwakePatch
    {
        public static void Prefix(CollectPlacedElementModule __instance, ModuleState state)
        {
            TargetInfo currentTarget = __instance.Manager.CurrentTarget;
            Structure structure = currentTarget.Targeted.Structure;

            if (structure == null)
            {
                return;
            }

            StructureElement structureElement;
            if (!structure.TryGetRemoveableElement(currentTarget.LookAtPoint, out structureElement))
            {
                _extractedTargetStructure = structure;
                isNonRemovable = true;
                _extractedState = state;
                int childCount = structure.ElementRoot.childCount;
                _extractedTargetElement = structure.ElementRoot.GetChild(childCount - 1).GetComponent<StructureElement>();
                if(_extractedTargetElement == null) { RLog.Msg("Failed to fetch element..."); }

                _extractedConstructionManager = __instance.Manager;
                _extractedGenericGrabberTargetProvider = __instance._grabberTargetProvider;
                return;
            } 
            else
            {
                _extractedTargetStructure = null;
                _extractedTargetElement = null;
                _extractedState = null;
                isNonRemovable = false;
                return;
            }
        }
    }

    public static void sendModVersion()
    {
        if (!BoltNetwork.isServerOrNotRunning)
        {
            RLog.Msg("User is not host, aborting mod version message send");
            return;
        }
        NetworkManager.SendJoiningMessage(_modVersion);
    }
}

[RegisterTypeInIl2Cpp]
public class EventHandler : GlobalEventListener
{
    public static EventHandler Instance;

    public static void Create()
    {
        if (Instance)
            return;

        Instance = new GameObject("EventTestEventHandler").AddComponent<EventHandler>();
    }

    public override void Connected(BoltConnection connection)
    {
        RLog.Msg("Player connected");
        Force_Remove_Logs.sendModVersion();
    }
}

internal class StringMessageEvent : Packets.NetEvent
{
    public override string Id => "EventTest_StringMessageEvent";

    public void Send(string message, GlobalTargets target = GlobalTargets.Everyone)
    {
        var packet = NewPacket(message.Length * 2, target);

        packet.Packet.WriteString(message);

        Send(packet);
    }

    private void HandleReceivedString(string receivedString)
    {
        GameObject TargetStructure = GameObject.Find(receivedString);
        if(TargetStructure != null)
        {
            RLog.Msg("Found Target Structure! Destroyed!");
            GameObject.Destroy(TargetStructure);
        } else
        {
            RLog.Msg("Unable to find Target Structure!");
        } 
    }

    public override void Read(UdpPacket packet, BoltConnection fromConnection)
    {
        var receivedString = packet.ReadString();
        RLog.Msg($"Received string: {receivedString}");

        HandleReceivedString(receivedString);
    }
}

internal class JoiningPlayerEvent : Packets.NetEvent
{
    public override string Id => "EventTest_JoiningPlayerEvent";

    public void Send(string modVersion = null, GlobalTargets target = GlobalTargets.Everyone)
    {
        var packet = NewPacket(1 + (modVersion != null ? modVersion.Length + 2 : 0), target);

        if (modVersion != null)
        {
            packet.Packet.WriteString(modVersion);
        }

        Send(packet);
    }

    private void HandleReceivedData(string receivedString)
    {
        RLog.Msg("Recieved string in join event  " + receivedString);

        //No need to check if mod already enabled
        if (Force_Remove_Logs.forceDisableMod)
        {
            return;
        }

        if (receivedString != null && Force_Remove_Logs._modVersion == receivedString)
        {
            RLog.Msg("Mod exists at host, version is equal, activating mod!");
            Force_Remove_Logs.forceDisableMod = false; 
        } else
        {
            Force_Remove_Logs.disableReason = "mismatchVersion";
        }
    }

    public override void Read(UdpPacket packet, BoltConnection _)
    {
        var receivedString = packet.ReadString();
        RLog.Msg($"Received string: {receivedString}");

        HandleReceivedData(receivedString);
    }
}

internal class NetworkManager
{
    public static StringMessageEvent _stringMessageEvent;
    public static JoiningPlayerEvent _joiningPlayerEvent;

    public static void RegisterPackets()
    {
        _stringMessageEvent = new StringMessageEvent();
        _joiningPlayerEvent = new JoiningPlayerEvent();
        Packets.Register(_stringMessageEvent);
        Packets.Register(_joiningPlayerEvent);

    }

    public static void SendStringMessage(string message, GlobalTargets target = GlobalTargets.Everyone)
    {
        _stringMessageEvent.Send(message, target);
    }

    public static void SendJoiningMessage(string message, GlobalTargets target = GlobalTargets.Everyone)
    {
        _joiningPlayerEvent.Send(message, target);
    }
}

