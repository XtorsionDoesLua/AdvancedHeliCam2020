using CitizenFX.Core;
using CitizenFX.Core.Native;

namespace Fivem.Common.Client
{
  public abstract class ClientScript : BaseScript
  {
    public static Player ClientPlayer => Game.get_Player();

    public static Ped ClientPed => Game.get_PlayerPed();

    public object ClientCharacter => this.get_Exports().get_Item("GetCurrentCharacter");

    public static Vehicle ClientCurrentVehicle => ClientScript.ClientPed?.get_CurrentVehicle();

    public static Vehicle ClientLastVehicle => ClientScript.ClientPed?.get_LastVehicle();

    public static Vehicle GetClosestVehicleToClient(float limitRadius = 2f) => ClientScript.ClientPed.GetClosestVehicleToPed(limitRadius);

    public static void SetNetworkIdNetworked(int netId, bool canMigrate = false)
    {
      API.SetNetworkIdExistsOnAllMachines(netId, true);
      API.SetNetworkIdCanMigrate(netId, canMigrate);
      if (canMigrate)
        return;
      API.NetworkSetNetworkIdDynamic(netId, true);
    }

    public static bool IsOnScreenKeyboardActive() => API.UpdateOnscreenKeyboard() == 3;

    public static async void PlayManagedSoundFrontend(string soundName, string soundSet = null)
    {
      int soundId = Audio.PlaySoundFrontend(soundName, soundSet);
      while (!Audio.HasSoundFinished(soundId))
        await BaseScript.Delay(200);
      Audio.ReleaseSound(soundId);
    }

    protected ClientScript() => base.\u002Ector();

    public static class Hud
    {
      public static void DrawText2D(
        float x,
        float y,
        float scale,
        string text,
        int r,
        int g,
        int b,
        int a)
      {
        Minimap minimapAnchor = MinimapAnchor.GetMinimapAnchor();
        x = minimapAnchor.X + minimapAnchor.Width * x;
        y = minimapAnchor.Y - y;
        API.SetTextFont(4);
        API.SetTextProportional(false);
        API.SetTextScale(scale, scale);
        API.SetTextColour(r, g, b, a);
        API.SetTextDropshadow(0, 0, 0, 0, (int) byte.MaxValue);
        API.SetTextEdge(2, 0, 0, 0, (int) byte.MaxValue);
        API.SetTextDropShadow();
        API.SetTextOutline();
        API.SetTextEntry("STRING");
        API.AddTextComponentString(text);
        API.DrawText(x, y);
      }

      public static void DrawText3D(Vector3 pos, string text, bool drawBackground = true, int font = 4)
      {
        float num1 = 0.0f;
        float num2 = 0.0f;
        API.World3dToScreen2d((float) pos.X, (float) pos.Y, (float) pos.Z, ref num1, ref num2);
        API.SetTextScale(0.35f, 0.35f);
        API.SetTextFont(font);
        API.SetTextProportional(true);
        API.SetTextColour((int) byte.MaxValue, (int) byte.MaxValue, (int) byte.MaxValue, 215);
        API.SetTextEntry("STRING");
        API.SetTextCentre(true);
        API.AddTextComponentString(text);
        API.DrawText(num1, num2);
        if (!drawBackground)
          return;
        API.DrawRect(num1, num2 + 0.0125f, (float) text.Length / 300f, 0.03f, 41, 11, 41, 68);
      }

      public static void DrawImageNotification(
        string picture,
        int icon,
        string title,
        string subtitle,
        string message)
      {
        API.SetNotificationTextEntry("STRING");
        API.AddTextComponentString(message);
        API.SetNotificationMessage(picture, picture, true, icon, title, subtitle);
      }

      public static void DisplayChatMessage(string text) => BaseScript.TriggerEvent("chat:addMessage", new object[1]
      {
        (object) text
      });
    }
  }
}
