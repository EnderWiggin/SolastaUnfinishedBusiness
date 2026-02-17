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
}
