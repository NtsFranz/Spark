﻿<svelte:head>
	<title>Spark</title>
</svelte:head>

<style lang="scss">
	.showcase-button {
		overflow: hidden;
		background-color: #0005;
		color: black;
		text-shadow: 0 0 0.5em white;
		height: 6em;
		font-weight: bold;
		font-family: Inconsolata, sans-serif;

		& > img {
			position: absolute;
			filter: blur(.1em);
		}

		& > svg {
			position: absolute;
			color: #0003;
			transform: scale(6) translateX(-0.9em);
		}
	}

	.flex-container {
		flex-wrap: wrap;

		& > a {
			flex-basis: 30rem;
			flex-grow: 1;
		}
	}
</style>

<section class="hero is-medium">
	<div class="hero-body" style="background-color: #0003;padding: 4rem 1.5rem;">
		<div class="container has-text-centered">
			<h1>Spark</h1>
			<img src="/img/ignite_logo.png" style="height:15rem;" alt="Ignite Logo">
			<p class="subtitle" style="font-size: 1.2em;">
				<span style="color: var(--ignite-orange);">Spark</span> is a Windows desktop program that can
				record, process, and upload EchoVR data from either a local EchoVR client or an Oculus Quest on the
				same network. Check out the <a href="https://echopedia.gg/wiki/Spark" target="blank"> Echopedia
				page </a> for a full list of features.
			</p>
			<p>
				The stuff hosted here can be used to control various aspects of Spark, for overlays in OBS, or to make
				it easier to create your own overlays using data generated by Spark.
			</p>

		</div>
	</div>
</section>
<div class="content" style="max-width: 60em; margin: 5em auto;">

	<div class="field has-addons flex-container">

		<a href="/overlays">
			<button class="button is-large is-fullwidth showcase-button">Overlays
				<img src="/img/minimap.png" alt="Minimap">
			</button>
		</a>

		<!--		<a href="/overlays/match_setup" style="width: 100%;">-->
		<!--			<button class="button is-large is-fullwidth showcase-button">Match Setup<img src="/img/minimap.png"></button>-->
		<!--		</a>-->


		<a href="/local_database">
			<button class="button is-large is-fullwidth showcase-button">Local Database
				<svg style="width:24px;height:24px" viewBox="0 0 24 24">
					<path fill="currentColor"
					      d="M12,3C7.58,3 4,4.79 4,7C4,9.21 7.58,11 12,11C16.42,11 20,9.21 20,7C20,4.79 16.42,3 12,3M4,9V12C4,14.21 7.58,16 12,16C16.42,16 20,14.21 20,12V9C20,11.21 16.42,13 12,13C7.58,13 4,11.21 4,9M4,14V17C4,19.21 7.58,21 12,21C16.42,21 20,19.21 20,17V14C20,16.21 16.42,18 12,18C7.58,18 4,16.21 4,14Z"/>
				</svg>
			</button>
		</a>

		<a href="/websockets">
			<button class="button is-large is-fullwidth showcase-button">Websockets API
				<img style="filter: blur(.1em) brightness(4);z-index: -1;transform: rotate(-10deg) scale(1.3);"
				     src="/img/json_cover.png" alt="">
			</button>
		</a>

		<a href="/api">
			<button class="button is-large is-fullwidth showcase-button">Spark API
				<svg style="width:24px;height:24px" viewBox="0 0 24 24">
					<path fill="currentColor"
					      d="M12,15.5A3.5,3.5 0 0,1 8.5,12A3.5,3.5 0 0,1 12,8.5A3.5,3.5 0 0,1 15.5,12A3.5,3.5 0 0,1 12,15.5M19.43,12.97C19.47,12.65 19.5,12.33 19.5,12C19.5,11.67 19.47,11.34 19.43,11L21.54,9.37C21.73,9.22 21.78,8.95 21.66,8.73L19.66,5.27C19.54,5.05 19.27,4.96 19.05,5.05L16.56,6.05C16.04,5.66 15.5,5.32 14.87,5.07L14.5,2.42C14.46,2.18 14.25,2 14,2H10C9.75,2 9.54,2.18 9.5,2.42L9.13,5.07C8.5,5.32 7.96,5.66 7.44,6.05L4.95,5.05C4.73,4.96 4.46,5.05 4.34,5.27L2.34,8.73C2.21,8.95 2.27,9.22 2.46,9.37L4.57,11C4.53,11.34 4.5,11.67 4.5,12C4.5,12.33 4.53,12.65 4.57,12.97L2.46,14.63C2.27,14.78 2.21,15.05 2.34,15.27L4.34,18.73C4.46,18.95 4.73,19.03 4.95,18.95L7.44,17.94C7.96,18.34 8.5,18.68 9.13,18.93L9.5,21.58C9.54,21.82 9.75,22 10,22H14C14.25,22 14.46,21.82 14.5,21.58L14.87,18.93C15.5,18.67 16.04,18.34 16.56,17.94L19.05,18.95C19.27,19.03 19.54,18.95 19.66,18.73L21.66,15.27C21.78,15.05 21.73,14.78 21.54,14.63L19.43,12.97Z"/>
				</svg>
			</button>
		</a>
	</div>
</div>