<script lang="ts">
	import Minimap from '$lib/components/Minimap.svelte';
	import CompactMainBanner from '$lib/components/CompactMainBanner.svelte';
	import PlayerLists from '$lib/components/PlayerLists.svelte';
	import EventLog from '$lib/components/EventLog.svelte';
	import CompactMinimap from '$lib/components/CompactMinimap.svelte';
	import PlayerListOrange from '$lib/components/PlayerListOrange.svelte';
	import PlayerListBlue from '$lib/components/PlayerListBlue.svelte';
	import { onDestroy } from 'svelte';
	import { SparkWebsocket } from '$lib/js/spark_websocket.js';
	import type { Config } from '$lib/js/SparkConfig';
	import type { Frame } from '$lib/js/Frame';

	let sw = new SparkWebsocket();
	let config: typeof Config = {};
	let frame: Frame = null;
	sw.subscribe('overlay_config', (data) => {
		config = data;
	});
	sw.subscribe('frame_10hz', (data) => {
		frame = data;
	});

	onDestroy(() => sw.close());
</script>

<main>
	<div class="no-overflow">
		{#if config && config['visibility']}
			{#if config['visibility']['main_banner']}
				<div style="margin-top:2em;">
					<CompactMainBanner />
				</div>
			{/if}

			{#if config['visibility']['player_rosters']}
				<div style="position:absolute; top:2em;left:2em;">
					<PlayerListOrange {frame} />
				</div>
			{/if}
			{#if config['visibility']['player_rosters']}
				<div style="position:absolute; top:2em;right:2em;">
					<PlayerListBlue {frame} />
				</div>
			{/if}

			{#if config['visibility']['minimap']}
				<div style="position:absolute;bottom:2em;right:1em;">
					<Minimap {frame} />
				</div>
			{/if}

			{#if config['visibility']['compact_minimap']}
				<div style="position:absolute;bottom:2em;right:1em;">
					<CompactMinimap {frame} />
				</div>
			{/if}

			{#if config['visibility']['event_log']}
				<div style="position:absolute;left:1em;bottom:1em;width:50em;">
					<EventLog />
				</div>
			{/if}
		{/if}
	</div>
</main>

<style>
	.no-overflow {
		overflow: hidden;
		position: absolute;
		height: 100%;
		width: 100%;
		top: 0;
		left: 0;
	}
</style>
