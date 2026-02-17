using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SolastaUnfinishedBusiness.Api.Helpers;
internal static class UiHelpers
{
    internal static Vector2Int GetScreenResolution()
    {
        string[] resolutionStrings = ServiceRepository.GetService<IGraphicsSettingsService>().Resolution.Split(GraphicsResourceManager.ResolutionSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (resolutionStrings.Length >= 2)
        {
            int width = int.Parse(resolutionStrings[0].Trim());
            int height = int.Parse(resolutionStrings[1].Trim());
            return new Vector2Int(width, height);
        }
        return new Vector2Int(0, 0);
    }
    internal static float GetAspectRatio()
    {
        Vector2Int resolution = GetScreenResolution();
        return (float)resolution.x / resolution.y;
    }

    internal static Vector2 GetOverlayCanvasSize()
    {
        if (ServiceRepository.GetService<IGuiService>() is not GuiManager gui)
        {
            return new Vector2(1920, 1080); //Reference Resolution
        }
        return gui.overlayCanvas.GetComponent<RectTransform>().rect.size;
    }
}
