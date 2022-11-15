<script lang="ts">
	import { onDestroy, onMount } from 'svelte';
	import ShowSpeed from '$lib/components/ShowSpeed.svelte';
	let speed = 0;

	let si: ReturnType<typeof setInterval>;
	onMount(() => {
		si = setInterval(() => {
			fetch('http://127.0.0.1:6723/le2/speed/')
				.then((resp) => resp.json())
				.then((resp) => {
					speed = resp['speed'];
				})
				.catch(() => {});
		}, 33);
	});

	onDestroy(() => {
		if (si) {
			clearInterval(si);
		}
	});
</script>

<svelte:head>
	<title>Player Speed</title>
</svelte:head>

<ShowSpeed {speed} />
