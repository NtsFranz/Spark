<script>
	import {onDestroy} from "svelte";
	import {SparkWebsocket} from '$lib/js/spark_websocket.js';
	import ThreeDee from "$lib/components/ThreeDee.svelte";


	let sw = new SparkWebsocket();
	let config = {};
	sw.subscribe("overlay_config", data => {
		config = data;
	});

	onDestroy(() => sw.close());
</script>

<main>
	<div class="no-overflow">
		<ThreeDee/>
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
