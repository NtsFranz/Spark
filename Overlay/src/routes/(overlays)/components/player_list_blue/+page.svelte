<script>
	import PlayerListBlue from '$lib/components/PlayerListBlue.svelte';
	import { SparkWebsocket } from '$lib/js/spark_websocket.js';
	import { onDestroy } from 'svelte';

	let frame;

	let sw = new SparkWebsocket();
	sw.subscribe('frame_10hz', (data) => {
		frame = data;
	});

	onDestroy(() => sw.close());
</script>

<svelte:head>
	<title>Blue Player List</title>
</svelte:head>

<PlayerListBlue {frame} />
