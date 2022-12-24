<script lang="ts">
	import { SparkWebsocket } from '$lib/js/spark_websocket.js';
	import { onDestroy } from 'svelte';
	import type { Config } from '$lib/js/SparkConfig';

	let orangeJoustVisible = false;
	let blueJoustVisible = false;

	let orangeJoustText = '';
	let blueJoustText = '';

	let config: typeof Config | null = null;

	let sw = new SparkWebsocket();
	sw.subscribe('joust', (data) => {
		if (data['team_color'] === 'orange') {
			orangeJoustVisible = true;
			orangeJoustText = Math.round(data['joust_time'] * 100) / 100 + ' s';
			// hide it after a delay
			setTimeout(function () {
				orangeJoustVisible = false;
			}, 10000);
		} else if (data['team_color'] === 'blue') {
			blueJoustVisible = true;
			blueJoustText = Math.round(data['joust_time'] * 100) / 100 + ' s';
			// hide it after a delay
			setTimeout(function () {
				blueJoustVisible = false;
			}, 10000);
		}
	});
	sw.subscribe('overlay_config', (data: typeof Config) => {
		config = data;
	});
	sw.subscribe('pause', (data) => {
		if (data['paused_state'] !== 'paused') return;

		if (data['paused_requested_team'] === 'orange') {
			orangeJoustVisible = true;
			orangeJoustText = 'PAUSED';
			// hide it after a delay
			setTimeout(function () {
				orangeJoustVisible = false;
			}, 10000);
		} else if (data['paused_requested_team'] === 'blue') {
			blueJoustVisible = true;
			blueJoustText = 'PAUSED';
			// hide it after a delay
			setTimeout(function () {
				blueJoustVisible = false;
			}, 10000);
		}
	});

	onDestroy(() => sw.close());
</script>

<svelte:head>
	<title>Midmatch Overlay</title>
</svelte:head>
<div class="container">
	<div class="top_row">
		<div id="team_name_orange" class="team_name orange">
			{config ? config['teams'][1]['team_name'] : ''}
		</div>
		<img
			id="team_logo_orange"
			class="team_logo orange"
			src={config
				? config['teams'][1]['team_logo']
				: 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII='}
			alt=""
		/>
		<div id="orange_joust" class="joust orange" class:visible={orangeJoustVisible}>
			<span>{orangeJoustText}</span>
		</div>

		<div id="team_name_blue" class="team_name blue">
			{config ? config['teams'][0]['team_name'] : ''}
		</div>
		<img
			id="team_logo_blue"
			class="team_logo blue"
			src={config
				? config['teams'][0]['team_logo']
				: 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII='}
			alt=""
		/>
		<div id="blue_joust" class="joust blue" class:visible={blueJoustVisible}>
			<span>{blueJoustText}</span>
		</div>
	</div>
</div>

<style>
	:root {
		margin: 0;
		font-family: 'Inconsolata', monospace;
		overflow: hidden;
	}

	.container {
		margin: 0;
		width: 100%;
	}

	.top_row {
		width: 100%;
	}

	.team_logo {
		width: 6em;
		height: 6em;
		position: absolute;
		top: -0.6em;
		border: 0.4em solid blue;
		border-top-width: 1em;
		border-radius: 0.5em;
	}

	.team_logo.orange {
		left: -0.6em;
		border-color: #d4791a;
		border-left-width: 1em;
		background-color: #936230;
	}

	.team_logo.blue {
		right: -0.6em;
		border-color: #2981df;
		border-right-width: 1em;
		background-color: #426b97;
	}

	.team_name {
		font-size: 2.6em;
		font-weight: 900;
		text-align: center;
		color: #ddd;
		position: absolute;
		top: 0;
		padding: 1.88rem;
		height: 0.3rem;
		line-height: 0;
		text-shadow: 0.07em 0.07em #0005;
		text-transform: uppercase;
		border-bottom: 3px solid #ccca;
	}

	.team_name::after,
	.team_name::before {
		content: '';
		display: block;
		position: absolute;
		width: 1.5em;
		height: 1.6em;
		top: 0;
	}

	.team_name.orange::after,
	.team_name.blue::before {
		background: linear-gradient(90deg, rgba(0, 0, 0, 0.25) 0%, rgba(0, 0, 0, 0) 100%);
		left: 0;
	}

	.team_name.blue::after,
	.team_name.orange::before {
		background: linear-gradient(90deg, rgba(0, 0, 0, 0) 0%, rgba(0, 0, 0, 0.25) 100%);
		right: 0;
	}

	.team_name.orange {
		background-color: #d4791a;
		left: 6.4rem;
		width: 36.65rem;
	}

	.team_name.blue {
		background-color: #2981df;
		right: 6.4rem;
		width: 36.7rem;
	}

	.joust {
		border: 0.13rem solid #ccc;
		background: #0004 linear-gradient(0deg, rgba(0, 0, 0, 0.25) 0%, rgba(0, 0, 0, 0.5) 100%);
		opacity: 0;
		position: absolute;
		top: 5rem;
		width: 11rem;
		height: 3rem;
		display: flex;
		justify-content: center;
		align-items: center;
		font-size: 1.7em;
		text-shadow: 0.07em 0.07em #0005;
		color: #eee;
		transition: all 0.5s;
	}

	.joust.orange {
		left: 33rem;
	}

	.joust.blue {
		right: 33rem;
	}

	.joust > span {
		display: inline-block;
		vertical-align: middle;
		line-height: normal;
	}

	.joust.visible {
		opacity: 1;
	}
</style>
