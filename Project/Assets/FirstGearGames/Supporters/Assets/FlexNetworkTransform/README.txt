FlexNetworkTransform

	API:

        /// <summary>
        /// Sets which platform the transform is on.
        /// </summary>
        /// <param name="platform"></param>
        public void SetPlatform(NetworkIdentity platform)

                /* While using client authoritative you may set the platform from the client
                * to enable perfect synchronization on moving platforms. When exiting the platform
                * you will call SetPlatform(null) to indicate the transform is no longer on the
                * platform. See Platforms demo for an example. */



	    /// <summary>
	    /// Dispatched when server receives data from a client while using client authoritative.
	    /// </summary>
		OnClientDataReceived

                /* To reject data on the server you only have to nullify
                 * the data. For example: obj.Data = null; */

                /* You may also modify the data instead.
                 * For example: obj.Data.Position = Vector3.zero; */

                /* Be aware that data may arrive as LocalSpace or WorldSpace
                 * depending on your FNT settings. When modifying data be sure to
                 * convert when necessary. */

                /* You could even implement your own way of snapping the client
                 * authoritative player back after rejecting the data. In my example
                 * I send the current coordinates of the transform back to the client
                 * in which they teleport to these values. */

                 /* Be aware that the space may change to world space if the client is on a platform.
                 * You can access the platform information within FlexNetworkTransform.Platform. If the
                 * Identity field is null, the player is not on a platform. */

		See ClientDataRejector in demos for an example.
	