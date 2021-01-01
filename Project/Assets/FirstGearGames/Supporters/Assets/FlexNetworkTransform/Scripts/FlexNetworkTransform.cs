using FirstGearGames.Utilities.Networks;
using FirstGearGames.Utilities.Objects;
using Mirror;
using UnityEngine;

namespace FirstGearGames.Mirrors.Assets.FlexNetworkTransforms
{

    [DisallowMultipleComponent]
    public class FlexNetworkTransform : FlexNetworkTransformBase
    {
        #region Public.
        /// <summary>
        /// Transform to synchronize.
        /// </summary>
        public override Transform TargetTransform => base.transform;
        #endregion


        protected override void OnEnable()
        {
            base.OnEnable();
        }
        protected override void OnDisable()
        {
            base.OnDisable();
        }

        public override bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            if (initialState)
            {
                /* If root then no need to send transform data as that's already
                * handled in the spawn message. */
                if (transform.root == null)
                    return base.OnSerialize(writer, initialState);

                writer.WriteVector3(TargetTransform.GetPosition(base.UseLocalSpace));
                writer.WriteUInt32(Quaternions.CompressQuaternion(TargetTransform.GetRotation(base.UseLocalSpace)));
                writer.WriteVector3(TargetTransform.GetScale());
            }
            return base.OnSerialize(writer, initialState);
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            if (initialState)
            {
                /* If root then no need to read transform data as that's already
                * handled in the spawn message. */
                if (transform.root == null)
                {
                    base.OnDeserialize(reader, initialState);
                    return;
                }

                TargetTransform.SetPosition(base.UseLocalSpace, reader.ReadVector3());
                TargetTransform.SetRotation(base.UseLocalSpace, Quaternions.DecompressQuaternion(reader.ReadUInt32()));
                TargetTransform.SetScale(reader.ReadVector3());
            }
            base.OnDeserialize(reader, initialState);
        }

        /// <summary>
        /// Sets which platform this transform is on.
        /// </summary>
        /// <param name="platform"></param>
        public void SetPlatform(NetworkIdentity platform)
        {
            base.SetPlatformInternal(platform);
        }

    }
}

