using System;
using UnityEngine;

namespace Dice.UnityContent
{
    /**
     * A content asset identified by its asset GUID. The runtime has no asset database, so the asset stores a copy of
     * that GUID, which the editor keeps equal to the GUID in the asset's .meta file when the asset is created,
     * imported, duplicated or moved (Dice.Editor.ContentAssetGuidPostprocessor).
     */
    public abstract class ContentAsset : ScriptableObject
    {
        /** The serialized name of the stored GUID, for the editor that keeps it. */
        public const string AssetGuidField = nameof(assetGuid);

        // Unity cannot serialize System.Guid. The GUID is stored as its 32 hexadecimal digits ("N"), the text the
        // .meta file's guid line holds, so the two can be compared directly.
        [SerializeField, HideInInspector] private string assetGuid;

        /** The asset's GUID; a missing or malformed stored GUID fails, naming the asset. */
        protected Guid AssetGuid
        {
            get
            {
                if (!Guid.TryParseExact(assetGuid, "N", out Guid value) || value == Guid.Empty)
                    throw new InvalidOperationException("The content asset " + name + " has no valid stored GUID: '" + assetGuid + "'.");
                return value;
            }
        }
    }
}
