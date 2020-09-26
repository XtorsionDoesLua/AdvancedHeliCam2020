using CitizenFX.Core;
using CitizenFX.Core.Native;
using CitizenFX.Core.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

namespace HeliCam
{
  public class Client : ClientScript
  {
    private const Control CAM_TOGGLE = (Control) 51;
    private const Control VISION_TOGGLE = (Control) 225;
    private const Control REPEL = (Control) 154;
    private const Control TOGGLE_ENTITY_LOCK = (Control) 255;
    private const Control TOGGLE_SPOTLIGHT = (Control) 75;
    private readonly HashSet<Blip> _markers = new HashSet<Blip>();
    private readonly Config config;
    private bool _helicam;
    private bool _calculateSpeed;
    private bool _roadOverlay;
    private bool _spotlightActive;
    private bool _shouldRappel;
    private float _fov = 80f;
    private int _visionState;
    private string _nearestPostal = "";
    private static string _nearestTgtPostal = "";
    private DateTime _lastTgtPostal = new DateTime(2000, 1, 1);
    private Minimap _playerMap;
    private readonly Dictionary<string, List<Vector3>> _streetOverlay;
    private readonly Dictionary<int, Spotlight> _drawnSpotlights = new Dictionary<int, Spotlight>();
    private double _lastCamHeading;
    private double _lastCamTilt;
    private Tuple<int, Vector3> _speedMarker;
    private readonly List<ThermalBone> _thermalBones = new List<ThermalBone>()
    {
      new ThermalBone("BONETAG_SPINE1", new Vector3(-0.25f), new Vector3(0.19f, 0.15f, 0.49f)),
      new ThermalBone("wheel_lf", new Vector3(-0.3f), new Vector3(0.3f), ThermalType.WHEEL),
      new ThermalBone("wheel_rf", new Vector3(-0.3f), new Vector3(0.3f), ThermalType.WHEEL),
      new ThermalBone("wheel_lr", new Vector3(-0.3f), new Vector3(0.3f), ThermalType.WHEEL),
      new ThermalBone("wheel_rr", new Vector3(-0.3f), new Vector3(0.3f), ThermalType.WHEEL),
      new ThermalBone("exhaust", new Vector3(-0.3f, -0.3f, -0.05f), new Vector3(0.3f, 0.3f, 0.2f), ThermalType.ENGINE)
    };
    private Vector3 _dummy = new Vector3(-0.3f);
    private readonly Func<string, string> CallbackFunction = HeliCam.Client.\u003C\u003Ec.\u003C\u003E9__25_0 ?? (HeliCam.Client.\u003C\u003Ec.\u003C\u003E9__25_0 = new Func<string, string>((object) HeliCam.Client.\u003C\u003Ec.\u003C\u003E9, __methodptr(\u003C\u002Ector\u003Eb__25_0)));

    public Client()
    {
      string str1 = API.LoadResourceFile(API.GetCurrentResourceName(), "streets.json") ?? "[]";
      try
      {
        this._streetOverlay = JsonConvert.DeserializeObject<Dictionary<string, List<Vector3>>>(str1.Trim());
      }
      catch (Exception ex)
      {
        Debug.WriteLine("Unable to read streets file: " + ex.Message);
        Debug.WriteLine(ex.StackTrace);
        Debug.WriteLine("Disabling map overlay");
        this._streetOverlay = new Dictionary<string, List<Vector3>>();
      }
      string str2 = API.LoadResourceFile(API.GetCurrentResourceName(), "config.json") ?? "[]";
      try
      {
        this.config = JsonConvert.DeserializeObject<Config>(str2);
      }
      catch (Exception ex)
      {
        Debug.WriteLine("Error reading config file. ");
        Debug.WriteLine(ex.Message);
        Debug.WriteLine(ex.StackTrace);
        Debug.WriteLine("Using safe configuration");
        this.config = new Config();
        this.config.LoadBackupConfig();
      }
    }

    [Command("heli")]
    internal void HeliCommand(int src, List<object> args, string raw)
    {
      if (args.Count == 0)
      {
        BaseScript.TriggerEvent("chat:addMessage", (object[]) new object[1]
        {
          (object) new
          {
            args = new string[1]
            {
              "[^1HeliCam^7] Usage: /heli [option]. Available options: clear, reset, help."
            }
          }
        });
      }
      else
      {
        string lower = ((object) args[0]).ToString().ToLower();
        if (lower == "help")
          API.SendNuiMessage(JsonConvert.SerializeObject((object) new
          {
            type = "help"
          }));
        else if (lower == "clear")
        {
          if (this._calculateSpeed)
            Game.SetControlNormal(0, (Control) 349, 200f);
          if (this._markers.Count != 0)
          {
            HashSet<Blip>.Enumerator enumerator = this._markers.GetEnumerator();
            try
            {
              while (enumerator.MoveNext())
                ((PoolObject) enumerator.Current).Delete();
            }
            finally
            {
              enumerator.Dispose();
            }
            this._markers.Clear();
          }
          BaseScript.TriggerServerEvent("helicam:removeAllMarkers", (object[]) new object[1]
          {
            (object) (int) (Game.get_PlayerPed().IsSittingInVehicle() ? ((Entity) Game.get_PlayerPed().get_CurrentVehicle()).get_NetworkId() : 0)
          });
        }
        else
        {
          if (!(lower == "reset"))
            return;
          if (this._helicam)
            ClientScript.PlayManagedSoundFrontend("SELECT", "HUD_FRONTEND_DEFAULT_SOUNDSET");
          this._helicam = false;
        }
      }
    }

    [Tick]
    internal async Task EveryTick()
    {
      HeliCam.Client client = this;
      foreach (KeyValuePair<int, Spotlight> keyValuePair in (KeyValuePair<int, Spotlight>[]) Enumerable.ToArray<KeyValuePair<int, Spotlight>>((IEnumerable<M0>) client._drawnSpotlights))
      {
        if (Player.op_Equality(client.get_Players().get_Item(keyValuePair.Key), (Player) null))
        {
          client._drawnSpotlights.Remove(keyValuePair.Key);
          return;
        }
        if ((double) World.GetDistance(((Entity) Game.get_PlayerPed()).get_Position(), keyValuePair.Value.Start) < 1000.0)
          API.DrawSpotLightWithShadow((float) keyValuePair.Value.Start.X, (float) keyValuePair.Value.Start.Y, (float) keyValuePair.Value.Start.Z, (float) keyValuePair.Value.End.X, (float) keyValuePair.Value.End.Y, (float) keyValuePair.Value.End.Z, (int) byte.MaxValue, 175, 110, 1000f, 10f, 0.0f, keyValuePair.Value.Radius, 1f, keyValuePair.Value.VehicleId);
      }
      HashSet<Blip>.Enumerator enumerator = client._markers.GetEnumerator();
      try
      {
        while (enumerator.MoveNext())
          World.DrawMarker((MarkerType) 25, enumerator.Current.get_Position(), (Vector3) Vector3.Zero, (Vector3) Vector3.Zero, new Vector3(10f), Color.FromArgb(175, 59, 231), false, false, false, (string) null, (string) null, false);
      }
      finally
      {
        enumerator.Dispose();
      }
      int num = await (Task<int>) Task.FromResult<int>((M0) 0);
    }

    [Tick]
    internal async Task UpdateCache()
    {
      this._playerMap = MinimapAnchor.GetMinimapAnchor();
      await BaseScript.Delay(2500);
    }

    [Tick]
    internal async Task ThermalTick()
    {
      HeliCam.Client client = this;
      if (client._visionState == 2)
      {
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: method pointer
        // ISSUE: reference to a compiler-generated method
        ((List<Vehicle>) Enumerable.ToList<Vehicle>(Enumerable.Where<Vehicle>((IEnumerable<M0>) World.GetAllVehicles(), (Func<M0, bool>) (HeliCam.Client.\u003C\u003Ec.\u003C\u003E9__29_0 ?? (HeliCam.Client.\u003C\u003Ec.\u003C\u003E9__29_0 = new Func<Vehicle, bool>((object) HeliCam.Client.\u003C\u003Ec.\u003C\u003E9, __methodptr(\u003CThermalTick\u003Eb__29_0))))))).ForEach(new Action<Vehicle>(client.\u003CThermalTick\u003Eb__29_1));
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        // ISSUE: method pointer
        // ISSUE: reference to a compiler-generated method
        ((List<Ped>) Enumerable.ToList<Ped>(Enumerable.Where<Ped>((IEnumerable<M0>) World.GetAllPeds(), (Func<M0, bool>) (HeliCam.Client.\u003C\u003Ec.\u003C\u003E9__29_2 ?? (HeliCam.Client.\u003C\u003Ec.\u003C\u003E9__29_2 = new Func<Ped, bool>((object) HeliCam.Client.\u003C\u003Ec.\u003C\u003E9, __methodptr(\u003CThermalTick\u003Eb__29_2))))))).ForEach(new Action<Ped>(client.\u003CThermalTick\u003Eb__29_3));
      }
      else
        await BaseScript.Delay(250);
    }

    [Tick]
    internal async Task MainTick()
    {
      HeliCam.Client client1 = this;
      Ped player = Game.get_PlayerPed();
      if (client1.IsPlayerInHeli() && (double) ((Entity) player.get_CurrentVehicle()).get_HeightAboveGround() > 2.5)
      {
        Vehicle currentVehicle = player.get_CurrentVehicle();
        if (Controls.IsControlJustPressed((Control) 51) && client1.config.AllowCamera && !Controls.IsControlPressed((Control) 25))
        {
          ClientScript.PlayManagedSoundFrontend("SELECT", "HUD_FRONTEND_DEFAULT_SOUNDSET");
          client1._helicam = true;
        }
        if (Controls.IsControlJustPressed((Control) 154) && client1.config.AllowRappel)
        {
          if (Entity.op_Equality((Entity) currentVehicle.GetPedOnSeat((VehicleSeat) 1), (Entity) player) || Entity.op_Equality((Entity) currentVehicle.GetPedOnSeat((VehicleSeat) 2), (Entity) player))
          {
            if (client1._shouldRappel)
            {
              ClientScript.PlayManagedSoundFrontend("SELECT", "HUD_FRONTEND_DEFAULT_SOUNDSET");
              API.TaskRappelFromHeli(((PoolObject) player).get_Handle(), 1);
            }
            else
            {
              Screen.ShowNotification("Press again to rappel from helicopter.", false);
              client1._shouldRappel = true;
            }
          }
          else
          {
            Screen.ShowNotification("~r~Can't rappel from this seat!", true);
            ClientScript.PlayManagedSoundFrontend("5_Second_Timer", "DLC_HEISTS_GENERAL_FRONTEND_SOUNDS");
          }
        }
      }
      else
        client1._shouldRappel = false;
      if (!client1._helicam)
        return;
      BaseScript.TriggerEvent("heliCam", (object[]) new object[1]
      {
        (object) true
      });
      API.SetTimecycleModifier("heliGunCam");
      API.SetTimecycleModifierStrength(0.3f);
      Scaleform scaleform = new Scaleform("HELI_CAM");
      while (!scaleform.get_IsLoaded())
        await BaseScript.Delay(1);
      Vehicle heli = player.get_CurrentVehicle();
      Camera cam = new Camera(API.CreateCam("DEFAULT_SCRIPTED_FLY_CAMERA", true));
      cam.AttachTo((Entity) heli, new Vector3(0.0f, 0.0f, -1.5f));
      cam.set_FieldOfView(client1._fov);
      cam.set_Rotation(new Vector3(0.0f, 0.0f, ((Entity) heli).get_Heading()));
      API.RenderScriptCams(true, false, 0, true, false);
      API.SendNuiMessage(JsonConvert.SerializeObject((object) new
      {
        shown = true,
        heli = ((Entity) heli).get_Model().get_IsHelicopter(),
        plane = !((Entity) heli).get_Model().get_IsHelicopter()
      }));
      BaseScript.TriggerEvent("HideHud", (object[]) new object[0]);
      Entity lockedEntity = (Entity) null;
      Vector3 hitPos = (Vector3) Vector3.Zero;
      Vector3 endPos = (Vector3) Vector3.Zero;
      Blip speedBlip = (Blip) null;
      Blip crosshairs = World.CreateBlip(((Entity) heli).get_Position());
      crosshairs.set_Sprite((BlipSprite) 123);
      crosshairs.set_Color((BlipColor) 1);
      crosshairs.set_Scale(0.5f);
      crosshairs.set_Name("Current Crosshair Position");
      crosshairs.set_Rotation(0);
      DateTime lastLosTime = DateTime.Now;
      DateTime lockedTime = DateTime.Now;
      DateTime enterTime = DateTime.Now;
      API.SetNetworkIdExistsOnAllMachines(((Entity) heli).get_NetworkId(), true);
      while (client1._helicam && ((Entity) player).get_IsAlive() && (player.IsSittingInVehicle() && Entity.op_Equality((Entity) player.get_CurrentVehicle(), (Entity) heli)) && (double) ((Entity) player.get_CurrentVehicle()).get_HeightAboveGround() > 2.5)
      {
        float zoom = (float) (1.0 / ((double) client1.config.FovMax - (double) client1.config.FovMin) * ((double) client1._fov - (double) client1.config.FovMin));
        Game.DisableControlThisFrame(0, (Control) 0);
        Game.DisableControlThisFrame(0, (Control) 99);
        Game.DisableControlThisFrame(0, (Control) 80);
        Game.DisableControlThisFrame(0, (Control) 74);
        Game.DisableControlThisFrame(0, (Control) 154);
        Game.DisableControlThisFrame(0, (Control) 75);
        Game.DisableControlThisFrame(0, (Control) 27);
        heli.set_IsRadioEnabled(false);
        if (Controls.IsControlJustPressed((Control) 51))
        {
          ClientScript.PlayManagedSoundFrontend("SELECT", "HUD_FRONTEND_DEFAULT_SOUNDSET");
          client1._helicam = false;
        }
        if (Controls.IsControlJustPressed((Control) 225))
        {
          ClientScript.PlayManagedSoundFrontend("SELECT", "HUD_FRONTEND_DEFAULT_SOUNDSET");
          client1.ChangeVision();
        }
        if (Controls.IsControlJustPressed((Control) 52))
        {
          cam.set_Rotation(new Vector3(0.0f, 0.0f, ((Entity) heli).get_Heading()));
          client1._fov = 80f;
        }
        if (Controls.IsControlJustPressed((Control) 0))
        {
          client1._roadOverlay = !client1._roadOverlay;
          if (client1._roadOverlay && client1._streetOverlay.Count == 0)
            API.SendNuiMessage(JsonConvert.SerializeObject((object) new
            {
              type = "alert",
              message = "Street overlay failed to load. Contact the server owner(s)."
            }));
        }
        DateTime dateTime;
        if (Entity.op_Inequality(lockedEntity, (Entity) null))
        {
          if (Entity.Exists(lockedEntity))
          {
            if (lockedEntity.get_Model().get_IsPed() && !client1.config.AllowPedLocking || lockedEntity.get_IsInWater())
            {
              lockedEntity = (Entity) null;
            }
            else
            {
              if (API.HasEntityClearLosToEntity(((PoolObject) heli).get_Handle(), ((PoolObject) lockedEntity).get_Handle(), 17))
                lastLosTime = DateTime.Now;
              client1.RenderInfo(lockedEntity);
              hitPos = endPos = lockedEntity.get_Position();
              dateTime = DateTime.Now;
              TimeSpan timeSpan = dateTime.Subtract(lockedTime);
              string str = timeSpan.ToString("mm\\:ss");
              client1.RenderText(0.2f, 0.4f, "~g~Locked ~w~" + str);
              if ((double) World.GetDistance(lockedEntity.get_Position(), ((Entity) heli).get_Position()) <= (double) client1.config.MaxDist && !Controls.IsControlJustPressed((Control) (int) byte.MaxValue))
              {
                dateTime = DateTime.Now;
                timeSpan = dateTime.Subtract(lastLosTime);
                if (timeSpan.Seconds <= 5)
                  goto label_40;
              }
              dateTime = DateTime.Now;
              timeSpan = dateTime.Subtract(lastLosTime);
              Debug.WriteLine(string.Format("LOS: {0}. Dist: {1}", (object) (int) timeSpan.Seconds, (object) (double) Math.Round((double) World.GetDistance(lockedEntity.get_Position(), ((Entity) heli).get_Position()))));
              lockedEntity = (Entity) null;
              lockedTime = new DateTime();
              cam.StopPointing();
              ClientScript.PlayManagedSoundFrontend("5_Second_Timer", "DLC_HEISTS_GENERAL_FRONTEND_SOUNDS");
            }
          }
          else
            lockedEntity = (Entity) null;
        }
        else
        {
          client1.RenderText(0.2f, 0.4f, "~r~Unlocked");
          client1.CheckInputRotation(cam, zoom);
          Tuple<Entity, Vector3, Vector3> entityInView = client1.GetEntityInView(cam);
          endPos = entityInView.get_Item3();
          if (Entity.Exists(entityInView.get_Item1()))
          {
            client1.RenderInfo(entityInView.get_Item1());
            if (Controls.IsControlJustPressed((Control) (int) byte.MaxValue))
            {
              ClientScript.PlayManagedSoundFrontend("SELECT", "HUD_FRONTEND_DEFAULT_SOUNDSET");
              lockedEntity = entityInView.get_Item1();
              lockedTime = DateTime.Now;
              cam.PointAt(lockedEntity, (Vector3) null);
              lastLosTime = DateTime.Now;
            }
          }
          Vector3 vector3 = entityInView.get_Item2();
          if (!((Vector3) ref vector3).get_IsZero())
            hitPos = entityInView.get_Item2();
        }
label_40:
        if (((Vector3) ref hitPos).get_IsZero())
        {
          crosshairs.set_Alpha(0);
        }
        else
        {
          crosshairs.set_Alpha((int) byte.MaxValue);
          crosshairs.set_Position(hitPos);
        }
        if (Controls.IsControlJustPressed((Control) 349) && client1.config.AllowSpeedCalculations)
        {
          if (((Vector3) ref hitPos).get_IsZero())
          {
            API.SendNuiMessage(JsonConvert.SerializeObject((object) new
            {
              type = "alert",
              message = "You are not aiming at anything!"
            }));
          }
          else
          {
            client1._calculateSpeed = !client1._calculateSpeed;
            if (client1._calculateSpeed)
            {
              ref __Null local = ref hitPos.Z;
              // ISSUE: cast to a reference type
              // ISSUE: explicit reference operation
              // ISSUE: cast to a reference type
              // ISSUE: explicit reference operation
              ^(float&) ref local = ^(float&) ref local + 0.1f;
              speedBlip = World.CreateBlip(hitPos);
              speedBlip.set_Color((BlipColor) 42);
              speedBlip.set_Sprite((BlipSprite) 56);
              Blip blip = speedBlip;
              dateTime = DateTime.Now;
              string str = "Speed Marker " + dateTime.ToString("HH:MM:SS");
              blip.set_Name(str);
              API.SetBlipDisplay(((PoolObject) speedBlip).get_Handle(), 2);
              client1._speedMarker = new Tuple<int, Vector3>(Game.get_GameTime(), speedBlip.get_Position());
            }
            else
            {
              if (Blip.op_Inequality(speedBlip, (Blip) null))
                ((PoolObject) speedBlip).Delete();
              speedBlip = (Blip) null;
              client1._speedMarker = (Tuple<int, Vector3>) null;
            }
          }
        }
        dateTime = DateTime.Now;
        TimeSpan timeSpan1 = dateTime.Subtract(enterTime);
        HeliCam.Client client2 = client1;
        double num1 = (double) client1.config.TextY - 0.100000001490116;
        string[] strArray = new string[5];
        dateTime = DateTime.UtcNow;
        strArray[0] = dateTime.ToString("MM/dd/yyyy\nHH:mm:ssZ");
        strArray[1] = "\n~y~";
        strArray[2] = timeSpan1.ToString("mm\\:ss");
        strArray[3] = "\n~b~Postal: ";
        strArray[4] = client1._nearestPostal;
        string text = string.Concat(strArray);
        client2.RenderText(0.01f, (float) num1, text, 0.3f);
        float num2 = (float) ((Entity) heli).get_Position().Y;
        float num3 = (float) ((Entity) heli).get_Position().X;
        string str1 = "N";
        string str2 = "E";
        if ((double) num2 < 0.0)
        {
          str1 = "S";
          num2 = Math.Abs(num2);
        }
        if ((double) num3 < 0.0)
        {
          str2 = "W";
          num3 = Math.Abs(num3);
        }
        double num4 = 360.0 - Math.Round((double) ((Entity) heli).get_Heading());
        client1.RenderText(0.075f, (float) ((double) client1.config.TextY - 0.100000001490116), string.Format("Aircraft:\n{0} {1}\n{2} {3}\n{4}°  {5}ft", (object[]) new object[6]
        {
          (object) str1,
          (object) (double) Math.Round((double) num2, 2),
          (object) str2,
          (object) (double) Math.Round((double) num3, 2),
          (object) (double) num4,
          (object) (double) Math.Ceiling((double) ((Entity) heli).get_HeightAboveGround() * 3.28080010414124)
        }), 0.3f);
        client1.HandleZoom(cam);
        client1.RenderTargetPosInfo(hitPos);
        if (client1.config.AllowMarkers)
          client1.HandleMarkers(hitPos);
        client1.RenderRotation(heli, ((Vector3) ref hitPos).get_IsZero() ? endPos : hitPos, cam.get_Rotation());
        if (client1._roadOverlay && client1._streetOverlay.Count > 0)
          client1.RenderStreetNames(Vector3.op_Equality(hitPos, (Vector3) Vector3.Zero) ? ((Entity) heli).get_Position() : hitPos);
        if (Controls.IsControlJustPressedRegardless((Control) 75) && client1.config.AllowSpotlights)
        {
          client1._spotlightActive = !client1._spotlightActive;
          if (!client1._spotlightActive)
            BaseScript.TriggerServerEvent("helicam:spotlight:kill", (object[]) new object[0]);
          else
            API.SendNuiMessage(JsonConvert.SerializeObject((object) new
            {
              type = "info",
              message = "Spotlight turned on"
            }));
        }
        if (client1._spotlightActive && client1.config.AllowSpotlights)
        {
          Vector3 vector3 = Entity.Exists(lockedEntity) ? Vector3.op_Subtraction(lockedEntity.get_Position(), cam.get_Position()) : (!((Vector3) ref hitPos).get_IsZero() ? Vector3.op_Subtraction(hitPos, cam.get_Position()) : Vector3.op_Subtraction(endPos, cam.get_Position()));
          ((Vector3) ref vector3).Normalize();
          BaseScript.TriggerServerEvent("helicam:spotlight:draw", (object[]) new object[4]
          {
            (object) (int) ((Entity) heli).get_NetworkId(),
            (object) cam.get_Position(),
            (object) vector3,
            (object) 5f
          });
        }
        scaleform.CallFunction("SET_ALT_FOV_HEADING", (object[]) new object[3]
        {
          (object) (float) ((Entity) heli).get_Position().Z,
          (object) (float) zoom,
          (object) (float) cam.get_Rotation().Z
        });
        scaleform.Render2D();
        hitPos = (Vector3) Vector3.Zero;
        await BaseScript.Delay(0);
      }
      if (client1._spotlightActive)
      {
        client1._spotlightActive = false;
        BaseScript.TriggerServerEvent("helicam:spotlight:kill", (object[]) new object[0]);
      }
      API.SendNuiMessage(JsonConvert.SerializeObject((object) new
      {
        shown = false
      }));
      BaseScript.TriggerEvent("helicam", (object[]) new object[1]
      {
        (object) false
      });
      BaseScript.TriggerEvent("ShowHud", (object[]) new object[0]);
      client1._helicam = false;
      if (Blip.op_Inequality(speedBlip, (Blip) null))
        ((PoolObject) speedBlip).Delete();
      ((PoolObject) crosshairs).Delete();
      client1._speedMarker = (Tuple<int, Vector3>) null;
      client1._calculateSpeed = false;
      API.ClearTimecycleModifier();
      client1._visionState = 0;
      client1._fov = (float) (((double) client1.config.FovMax + (double) client1.config.FovMin) * 0.5);
      API.RenderScriptCams(false, false, 0, true, false);
      scaleform.Dispose();
      ((PoolObject) cam).Delete();
      Game.set_Nightvision(false);
      scaleform = (Scaleform) null;
      heli = (Vehicle) null;
      cam = (Camera) null;
      lockedEntity = (Entity) null;
      hitPos = (Vector3) null;
      endPos = (Vector3) null;
      speedBlip = (Blip) null;
      crosshairs = (Blip) null;
    }

    [EventHandler("onResourceStop")]
    internal void ResourceStopped(string resourceName)
    {
      if (!(resourceName == API.GetCurrentResourceName()))
        return;
      API.ClearTimecycleModifier();
      Game.set_Nightvision(false);
      Game.set_ThermalVision(false);
      BaseScript.TriggerEvent("ShowHud", (object[]) new object[0]);
      BaseScript.TriggerEvent("helicam", (object[]) new object[1]
      {
        (object) false
      });
    }

    [EventHandler("PostalDisplay:NearestPostal")]
    internal void NearestPostalEvent(string postal) => this._nearestPostal = postal;

    [EventHandler("helicam:deleteMarker")]
    internal void DeleteMarker(int src, Vector3 pos)
    {
      if (src == Game.get_Player().get_ServerId())
        return;
      HashSet<Blip>.Enumerator enumerator = this._markers.GetEnumerator();
      try
      {
        while (enumerator.MoveNext())
        {
          Blip current = enumerator.Current;
          if (Vector3.op_Equality(current.get_Position(), pos))
          {
            this._markers.Remove(current);
            ((PoolObject) current).Delete();
            break;
          }
        }
      }
      finally
      {
        enumerator.Dispose();
      }
    }

    [EventHandler("helicam:deleteAllMarkers")]
    internal void DeleteAllMarkers(int src, int vehId)
    {
      if (src == Game.get_Player().get_ServerId() || !Game.get_PlayerPed().IsSittingInVehicle() || ((Entity) Game.get_PlayerPed().get_CurrentVehicle()).get_NetworkId() != vehId)
        return;
      if (this._markers.Count == 0)
      {
        Debug.WriteLine("ERROR: our marker list is different from the one who sent the event");
      }
      else
      {
        HashSet<Blip>.Enumerator enumerator = this._markers.GetEnumerator();
        try
        {
          while (enumerator.MoveNext())
            ((PoolObject) enumerator.Current).Delete();
        }
        finally
        {
          enumerator.Dispose();
        }
        this._markers.Clear();
      }
    }

    [EventHandler("helicam:addMarker")]
    internal void AddMarker(int src, int vehId, Vector3 pos, string name)
    {
      if (src == Game.get_Player().get_ServerId() || !Game.get_PlayerPed().IsSittingInVehicle() || ((Entity) Game.get_PlayerPed().get_CurrentVehicle()).get_NetworkId() != vehId)
        return;
      Blip blip = World.CreateBlip(pos);
      blip.set_Sprite((BlipSprite) 123);
      blip.set_Name(name);
      blip.set_Color((BlipColor) 27);
      blip.set_Rotation(0);
      this._markers.Add(blip);
    }

    [EventHandler("helicam:drawSpotlight")]
    internal void RenderSpotlight(int src, int vehId, Vector3 start, Vector3 end, float size)
    {
      if (this._drawnSpotlights.Count > 0)
      {
        Dictionary<int, Spotlight>.Enumerator enumerator = this._drawnSpotlights.GetEnumerator();
        try
        {
          while (enumerator.MoveNext())
          {
            KeyValuePair<int, Spotlight> current = enumerator.Current;
            if (src != current.Key && vehId == current.Value.VehicleId)
            {
              Debug.WriteLine("spotlight already drawn!");
              this._spotlightActive = false;
              return;
            }
          }
        }
        finally
        {
          enumerator.Dispose();
        }
      }
      ref __Null local = ref start.Z;
      // ISSUE: cast to a reference type
      // ISSUE: explicit reference operation
      // ISSUE: cast to a reference type
      // ISSUE: explicit reference operation
      ^(float&) ref local = ^(float&) ref local - 5f;
      this._drawnSpotlights[src] = new Spotlight(vehId, start, end, size);
    }

    [EventHandler("helicam:killSpotlight")]
    internal void RemoveSpotlight(int src)
    {
      if (this._drawnSpotlights.ContainsKey(src))
        this._drawnSpotlights.Remove(src);
      if (src != Game.get_Player().get_ServerId())
        return;
      API.SendNuiMessage(JsonConvert.SerializeObject((object) new
      {
        type = "info",
        message = "Spotlight turned off"
      }));
    }

    private Tuple<Entity, Vector3, Vector3> GetEntityInView(Camera cam)
    {
      Vector3 position = cam.get_Position();
      Vector3 vec = this.RotAnglesToVec(cam.get_Rotation());
      Vector3 vector3 = Vector3.op_Addition(position, Vector3.op_Multiply(vec, this.config.MaxDist + 300f));
      RaycastResult raycastResult = World.Raycast(position, vector3, (IntersectOptions) -1, (Entity) Game.get_PlayerPed().get_CurrentVehicle());
      if (((RaycastResult) ref raycastResult).get_DitHitEntity() && (((RaycastResult) ref raycastResult).get_HitEntity().get_Model().get_IsVehicle() || ((RaycastResult) ref raycastResult).get_HitEntity().get_Model().get_IsPed()))
        return (Tuple<Entity, Vector3, Vector3>) Tuple.Create<Entity, Vector3, Vector3>((M0) ((RaycastResult) ref raycastResult).get_HitEntity(), (M1) ((RaycastResult) ref raycastResult).get_HitPosition(), (M2) vector3);
      return ((RaycastResult) ref raycastResult).get_DitHit() ? new Tuple<Entity, Vector3, Vector3>((Entity) null, ((RaycastResult) ref raycastResult).get_HitPosition(), vector3) : new Tuple<Entity, Vector3, Vector3>((Entity) null, (Vector3) Vector3.Zero, vector3);
    }

    private void ChangeVision()
    {
      if (this._visionState == 0)
      {
        API.ClearTimecycleModifier();
        API.SetTimecycleModifier("heliGunCam");
        API.SetTimecycleModifierStrength(0.3f);
        Game.set_Nightvision(true);
        this._visionState = 1;
      }
      else if (this._visionState == 1)
      {
        Game.set_Nightvision(false);
        API.SetTimecycleModifier("NG_blackout");
        API.SetTimecycleModifierStrength(0.992f);
        this._visionState = 2;
      }
      else
      {
        API.ClearTimecycleModifier();
        API.SetTimecycleModifier("heliGunCam");
        API.SetTimecycleModifierStrength(0.3f);
        this._visionState = 0;
      }
    }

    private void HandleZoom(Camera cam)
    {
      if (Controls.IsControlJustPressed((Control) 241) || Controls.IsControlPressed((Control) 188) || Controls.IsControlJustPressed((Control) 260) && Entity.op_Inequality((Entity) Game.get_PlayerPed().get_CurrentVehicle().get_Driver(), (Entity) Game.get_PlayerPed()))
        this._fov = Math.Max(this._fov - this.config.ZoomSpeed, this.config.FovMin);
      if (Controls.IsControlJustPressed((Control) 242) || Controls.IsControlPressed((Control) 187) || Controls.IsControlJustPressed((Control) 228) && Entity.op_Inequality((Entity) Game.get_PlayerPed().get_CurrentVehicle().get_Driver(), (Entity) Game.get_PlayerPed()))
        this._fov = Math.Min(this._fov + this.config.ZoomSpeed, this.config.FovMax);
      float fieldOfView = cam.get_FieldOfView();
      if ((double) Math.Abs(this._fov - fieldOfView) < 0.100000001490116)
        this._fov = fieldOfView;
      cam.set_FieldOfView(fieldOfView + (float) (((double) this._fov - (double) fieldOfView) * 0.0500000007450581));
    }

    private void HandleMarkers(Vector3 cam)
    {
      this.RenderText(0.125f, this.config.TextY - 0.1f, string.Format("Markers:  {0}", (object) (int) this._markers.Count), 0.3f);
      if (Controls.IsControlJustPressed((Control) 304))
      {
        if (this._markers.Count > 0)
        {
          Blip blip = (Blip) Enumerable.LastOrDefault<Blip>((IEnumerable<M0>) this._markers);
          BaseScript.TriggerServerEvent("helicam:removeMarker", (object[]) new object[1]
          {
            (object) blip.get_Position()
          });
          this._markers.Remove(blip);
          ((PoolObject) blip).Delete();
        }
        if (this._markers.Count == 0)
          BaseScript.TriggerServerEvent("helicam:removeAllMarkers", (object[]) new object[1]
          {
            (object) (int) ((Entity) Game.get_PlayerPed().get_CurrentVehicle()).get_NetworkId()
          });
      }
      if (!Controls.IsControlJustPressed((Control) 186))
        return;
      if (this._markers.Count > 9)
        API.SendNuiMessage(JsonConvert.SerializeObject((object) new
        {
          type = "alert",
          message = "You have reached your marker limit!"
        }));
      else if (((Vector3) ref cam).get_IsZero())
      {
        API.SendNuiMessage(JsonConvert.SerializeObject((object) new
        {
          type = "alert",
          message = "You are not aiming at anything!"
        }));
      }
      else
      {
        string str = string.Format("Marker #{0} - {1}", (object) (int) this._markers.Count, (object) DateTime.Now.ToString("H:mm"));
        ref __Null local = ref cam.Z;
        // ISSUE: cast to a reference type
        // ISSUE: explicit reference operation
        // ISSUE: cast to a reference type
        // ISSUE: explicit reference operation
        ^(float&) ref local = ^(float&) ref local + 0.01f;
        Blip blip = World.CreateBlip(cam);
        blip.set_Sprite((BlipSprite) 123);
        blip.set_Name(str);
        blip.set_Color((BlipColor) 27);
        blip.set_Rotation(0);
        API.SetBlipDisplay(((PoolObject) blip).get_Handle(), 2);
        this._markers.Add(blip);
        BaseScript.TriggerServerEvent("helicam:createMarker", (object[]) new object[3]
        {
          (object) (int) ((Entity) Game.get_PlayerPed().get_CurrentVehicle()).get_NetworkId(),
          (object) blip.get_Position(),
          (object) str
        });
      }
    }

    private void RenderStreetNames(Vector3 pos)
    {
      Dictionary<string, List<Vector3>>.Enumerator enumerator1 = this._streetOverlay.GetEnumerator();
      try
      {
        while (enumerator1.MoveNext())
        {
          KeyValuePair<string, List<Vector3>> current1 = enumerator1.Current;
          Dictionary<Vector3, double> dictionary = new Dictionary<Vector3, double>();
          List<Vector3>.Enumerator enumerator2 = current1.Value.GetEnumerator();
          try
          {
            while (enumerator2.MoveNext())
            {
              Vector3 current2 = enumerator2.Current;
              dictionary.Add(current2, (double) World.GetDistance(pos, current2));
            }
          }
          finally
          {
            enumerator2.Dispose();
          }
          List<KeyValuePair<Vector3, double>> list = (List<KeyValuePair<Vector3, double>>) Enumerable.ToList<KeyValuePair<Vector3, double>>((IEnumerable<M0>) dictionary);
          list.Sort((Comparison<KeyValuePair<Vector3, double>>) ((pair1, pair2) => pair1.Value.CompareTo(pair2.Value)));
          int num = 0;
          List<KeyValuePair<Vector3, double>>.Enumerator enumerator3 = list.GetEnumerator();
          try
          {
            while (enumerator3.MoveNext())
            {
              KeyValuePair<Vector3, double> current2 = enumerator3.Current;
              if (current2.Value < 300.0)
              {
                this.RenderText3D(current2.Key, current1.Key);
                ++num;
              }
            }
          }
          finally
          {
            enumerator3.Dispose();
          }
          if (num == 0)
          {
            KeyValuePair<Vector3, double> keyValuePair = (KeyValuePair<Vector3, double>) Enumerable.First<KeyValuePair<Vector3, double>>((IEnumerable<M0>) list);
            if (keyValuePair.Value < 500.0)
            {
              keyValuePair = (KeyValuePair<Vector3, double>) Enumerable.First<KeyValuePair<Vector3, double>>((IEnumerable<M0>) list);
              this.RenderText3D(keyValuePair.Key, current1.Key);
            }
          }
        }
      }
      finally
      {
        enumerator1.Dispose();
      }
    }

    private void RenderInfo(Entity ent)
    {
      if (!ent.get_Model().get_IsVehicle())
        return;
      Vehicle vehicle = (Vehicle) ent;
      this.RenderText(0.2f, this.config.TextY, "Model: " + vehicle.get_LocalizedName() + "\nPlate: " + vehicle.get_Mods().get_LicensePlate());
      this.RenderText(0.61f, this.config.TextY, (double) ((Entity) vehicle).get_Heading() < 45.0 ? "NB" : ((double) ((Entity) vehicle).get_Heading() < 135.0 ? "WB" : ((double) ((Entity) vehicle).get_Heading() < 225.0 ? "SB" : ((double) ((Entity) vehicle).get_Heading() < 315.0 ? "EB" : "NB"))));
    }

    private void RenderRotation(Vehicle veh, Vector3 target, Vector3 camRotation)
    {
      double num1 = Math.Round((270.0 - Math.Atan2((double) (((Entity) veh).get_Position().Y - target.Y), (double) (((Entity) veh).get_Position().X - target.X)) * 180.0 / Math.PI) % 360.0, 0) + Math.Round((double) ((Entity) veh).get_Heading());
      if (num1 > 360.0)
        num1 -= 360.0;
      if (this._lastCamHeading != num1)
      {
        API.SendNuiMessage(JsonConvert.SerializeObject((object) new
        {
          rotation = num1
        }));
        this._lastCamHeading = num1;
      }
      double num2 = Math.Round((double) camRotation.X, 0);
      if (this._lastCamTilt != num2)
      {
        this._lastCamTilt = num2;
        API.SendNuiMessage(JsonConvert.SerializeObject((object) new
        {
          camtilt = num2
        }));
      }
      double num3 = Math.Round((double) camRotation.Z);
      API.SendNuiMessage(JsonConvert.SerializeObject((object) new
      {
        northheading = (num3 > 0.0 ? 180.0 + (180.0 - num3) : Math.Abs(num3))
      }));
      float num4 = (float) target.Y;
      float num5 = (float) target.X;
      string str1 = "N";
      string str2 = "E";
      if ((double) num4 < 0.0)
      {
        str1 = "S";
        num4 = Math.Abs(num4);
      }
      if ((double) num5 < 0.0)
      {
        str2 = "W";
        num5 = Math.Abs(num5);
      }
      double num6 = 360.0 - Math.Round((double) camRotation.Z);
      if (num6 > 360.0)
        num6 -= 360.0;
      if (DateTime.Now.Subtract(this._lastTgtPostal).Seconds > 2)
      {
        this._lastTgtPostal = DateTime.Now;
        BaseScript.TriggerEvent("Postal:GetPostalAtCoords", (object[]) new object[2]
        {
          (object) target,
          (object) this.CallbackFunction
        });
      }
      this.RenderText(0.55f, (float) ((double) this.config.TextY - 0.100000001490116), string.Format("Map TGT:\n{0} {1}\n{2} {3}\n{4}°  ~y~Postal: {5}", (object[]) new object[6]
      {
        (object) str1,
        (object) (double) Math.Round((double) num4, 2),
        (object) str2,
        (object) (double) Math.Round((double) num5, 2),
        (object) (double) num6,
        (object) HeliCam.Client._nearestTgtPostal
      }), 0.3f);
    }

    private void RenderTargetPosInfo(Vector3 pos)
    {
      if (((Vector3) ref pos).get_IsZero())
        return;
      Vector3 vector3 = (Vector3) null;
      API.GetNthClosestVehicleNode((float) pos.X, (float) pos.Y, (float) pos.Z, 0, ref vector3, 0, 0, 0);
      uint num1 = 1;
      uint num2 = 1;
      API.GetStreetNameAtCoord((float) pos.X, (float) pos.Y, (float) pos.Z, ref num2, ref num1);
      string streetNameFromHashKey = API.GetStreetNameFromHashKey(num1);
      string str = !(streetNameFromHashKey != "") || !(streetNameFromHashKey != "NULL") || streetNameFromHashKey == null ? "" : "~t~ / " + streetNameFromHashKey;
      this.RenderText(0.64f, this.config.TextY, World.GetStreetName(pos) + "\n" + str);
      if (!this._calculateSpeed)
        return;
      double distance = (double) World.GetDistance(pos, this._speedMarker.get_Item2());
      int num3 = (Game.get_GameTime() - this._speedMarker.get_Item1()) / 1000;
      double d = distance / (double) num3 * 2.236936;
      if (double.IsInfinity(d) || double.IsNaN(d))
        this.RenderText(0.4f, this.config.TextY, string.Format("Est. Speed: Measuring\nTime: {0}s", (object) (int) num3), 0.4f);
      else
        this.RenderText(0.4f, this.config.TextY, string.Format("Est. Speed: {0}mph\nTime: {1}s", (object) (double) Math.Round(d, 0), (object) (int) num3), 0.4f);
      World.DrawMarker((MarkerType) 25, this._speedMarker.get_Item2(), (Vector3) Vector3.Zero, (Vector3) Vector3.Zero, new Vector3(10f), Color.FromArgb(109, 184, 215), false, false, false, (string) null, (string) null, false);
    }

    private void RenderText(float x, float y, string text, float scale = 0.5f)
    {
      API.SetTextFont(0);
      API.SetTextProportional(false);
      API.SetTextScale(0.0f, scale);
      API.SetTextColour((int) byte.MaxValue, (int) byte.MaxValue, (int) byte.MaxValue, (int) byte.MaxValue);
      API.SetTextDropshadow(0, 0, 0, 0, (int) byte.MaxValue);
      API.SetTextEdge(1, 0, 0, 0, (int) byte.MaxValue);
      API.SetTextDropShadow();
      API.SetTextOutline();
      API.SetTextEntry("STRING");
      API.AddTextComponentString(text);
      API.DrawText(this._playerMap.RightX + x, (float) ((double) this._playerMap.BottomY - (double) API.GetTextScaleHeight(y, 0) - 0.00499999988824129));
    }

    private void RenderText3D(Vector3 pos, string text)
    {
      float num1 = 0.0f;
      float num2 = 0.0f;
      API.World3dToScreen2d((float) pos.X, (float) pos.Y, (float) pos.Z, ref num1, ref num2);
      API.SetTextFont(0);
      API.SetTextScale(0.25f, 0.25f);
      API.SetTextProportional(false);
      API.SetTextColour((int) byte.MaxValue, (int) byte.MaxValue, (int) byte.MaxValue, (int) byte.MaxValue);
      API.SetTextDropshadow(10, 0, 0, 0, (int) byte.MaxValue);
      API.SetTextEdge(1, 0, 0, 0, (int) byte.MaxValue);
      API.SetTextDropShadow();
      API.SetTextOutline();
      API.SetTextEntry("STRING");
      API.AddTextComponentString(text);
      API.DrawText(num1, num2);
    }

    private Vector3 RotAnglesToVec(Vector3 rot)
    {
      double num1 = Math.PI * (double) rot.X / 180.0;
      double num2 = Math.PI * (double) rot.Z / 180.0;
      double num3 = Math.Abs(Math.Cos(num1));
      return new Vector3((float) (-Math.Sin(num2) * num3), (float) (Math.Cos(num2) * num3), (float) Math.Sin(num1));
    }

    private void CheckInputRotation(Camera cam, float zoom)
    {
      float disabledControlNormal1 = Game.GetDisabledControlNormal(0, (Control) 220);
      float disabledControlNormal2 = Game.GetDisabledControlNormal(0, (Control) 221);
      Vector3 rotation = cam.get_Rotation();
      if ((double) disabledControlNormal1 == 0.0 && (double) disabledControlNormal2 == 0.0)
        return;
      float num1 = (float) (rotation.Z + (double) disabledControlNormal1 * -1.0 * (double) this.config.SpeedUD * ((double) zoom + 0.100000001490116));
      float num2 = (float) Math.Max(Math.Min(20.0, (double) rotation.X + (double) disabledControlNormal2 * -1.0 * (double) this.config.SpeedLR * ((double) zoom + 0.1)), -89.5);
      cam.set_Rotation(new Vector3(num2, 0.0f, num1));
    }

    private bool IsPlayerInHeli()
    {
      Vehicle currentVehicle = Game.get_PlayerPed().get_CurrentVehicle();
      if (!Entity.Exists((Entity) currentVehicle))
        return false;
      return Game.get_PlayerPed().get_IsInHeli() || this.config.AircraftHashes.Contains(currentVehicle.get_DisplayName().ToLower()) || this.config.HelicopterHashes.Contains(currentVehicle.get_DisplayName().ToLower());
    }

    private void DrawThermal(float x1, float y1, float z1, float x2, float y2, float z2) => API.DrawBox(x1, y1, z1, x2, y2, z2, (int) byte.MaxValue, (int) byte.MaxValue, (int) byte.MaxValue, 90);
  }
}
