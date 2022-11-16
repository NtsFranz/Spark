<script lang="ts">
	import { frame, game_clock_display, last_score, mathematical_time } from '$lib/js/stores';
	import { overlay_config } from '$lib/js/stores.js';
	import { SparkWebsocket } from '$lib/js/spark_websocket.js';
	import DiagonalDropdown from '$lib/components/DiagonalDropdown.svelte';
	import { onDestroy } from 'svelte';

	let orangeGoalVisible = false;
	let blueGoalVisible = false;

	let orangeJoustVisible = false;
	let blueJoustVisible = false;
	let orangeJoustText = '';
	let blueJoustText = '';

	let mathematicalTimeVisible = false;
	let mathematicalTimeText = '';



	let sw = new SparkWebsocket();
	sw.subscribe('goal', (data) => {
		if (data['team_scored'] == 'blue') {
			blueGoalVisible = true;
			setTimeout(() => {
				blueGoalVisible = false;
			}, 10000);
		}
		if (data['team_scored'] == 'orange') {
			orangeGoalVisible = true;
			setTimeout(() => {
				orangeGoalVisible = false;
			}, 10000);
		}
	});

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

<div id="container">
	{#if $frame}
		<div id="main_banner">
			<div id="entire_clock_area">
				<!--			<img id="team_logo_orange" class="team_logo"/>-->
				<!--			<div id="team_name_orange" class="team_name"></div>-->

				<div class="center_clock_area">
					<div id="game_clock_display">{$game_clock_display}</div>

					<div class="possession orange">
						<div
							id="possession_orange"
							class={$frame['possession'][0] === 1 && $frame['game_status'] === 'playing'
								? 'active'
								: ''}
						/>
					</div>
					<div class="possession blue">
						<div
							id="possession_blue"
							class={$frame['possession'][0] === 0 && $frame['game_status'] === 'playing'
								? 'active'
								: ''}
						/>
					</div>
					<div id="round_scores_combined" class="round_scores" />
				</div>
				<div id="points_orange" class="points orange">{$frame['orange_points']}</div>

				<div class="moving_part_container orange">
					<div
						class="moving_part orange {$overlay_config &&
						$overlay_config['visibility']['show_team_names']
							? 'visible'
							: ''}"
					>
						<div
							class="team_logos_collapsible orange"
							class:visible={$overlay_config && $overlay_config['visibility']['show_team_logos']}
						>
							<div class="points secondary orange left" />
							<div
								id="team_logo_background_orange"
								class="team_logo_background orange"
								style="background-image: url('{$overlay_config
									? $overlay_config['teams'][1]['team_logo']
									: 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII='}');"
							/>
							<div class="points secondary orange right" />
						</div>
						<div class="banner orange">
							<div>
								<span>{$overlay_config ? $overlay_config['teams'][1]['team_name'] : ''}</span>
							</div>
						</div>
					</div>
				</div>

				<div id="points_blue" class="points blue">{$frame['blue_points']}</div>

				<div class="moving_part_container blue">
					<div
						id="blue_goal_banner"
						class="moving_part blue"
						class:visible={$overlay_config && $overlay_config['visibility']['show_team_names']}
					>
						<div class="banner blue">
							<div>
								<span>{$overlay_config ? $overlay_config['teams'][0]['team_name'] : ''}</span>
							</div>
						</div>

						<div
							class="team_logos_collapsible blue {$overlay_config &&
							$overlay_config['visibility']['show_team_logos']
								? 'visible'
								: ''}"
						>
							<div class="points secondary blue right" />
							<div
								id="team_logo_background_blue"
								class="team_logo_background blue"
								style="background-image: url('{$overlay_config
									? $overlay_config['teams'][0]['team_logo']
									: 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII='}');"
							/>
							<div class="points secondary blue left" />
						</div>
					</div>
				</div>

				<!--			<div id="team_name_blue" class="team_name"></div>-->
				<!--			<img id="team_logo_blue" class="team_logo"/>-->
			</div>
			{#if $overlay_config}
				<div class="round-wins-bottom">
					{#each Array($overlay_config['round_scores']['round_count']) as _, i}
						{#if $overlay_config['round_scores']['round_scores_orange'].length - 1 > i}
							<!-- Finished rounds -->
							<div>
								<div
									class={$overlay_config['round_scores']['round_scores_orange'][i] >
									$overlay_config['round_scores']['round_scores_blue'][i]
										? 'orange'
										: 'blue'}
								/>
							</div>
						{:else if $overlay_config['round_scores']['round_scores_orange'].length - 1 === i}
							<!-- Current round -->
							<div class="unfinished">
								<div
									class="orange"
									style="flex-grow:{$frame['orange_points']}"
								/>
								<div
									class="blue"
									style="flex-grow:{$frame['blue_points']}"
								/>
							</div>
						{:else}
							<!-- Future rounds -->
							<div />
						{/if}
					{/each}
				</div>

				<div style="position:absolute;left: -.6em;top: 4.1em;">
					<DiagonalDropdown
						visible={orangeGoalVisible}
						backgroundColor="#d57d14"
						borderColor="#9d6118"
						width="23.4em"
					>
						<div class="goal_area">
							<div class="goal_names_area">
								<div>{$frame['last_score']['goal_type']}</div>
								<div>{$frame['last_score']['person_scored']}</div>
							</div>
							<div class="speed">
								{$frame['last_score']['disc_speed'].toFixed(1)} m/s
								<br />
								{$frame['last_score']['distance_thrown'].toFixed(1)} m
							</div>
							<div class="point_value orange">{$frame['last_score']['point_amount']}</div>
						</div>
					</DiagonalDropdown>

					<DiagonalDropdown
						visible={blueGoalVisible}
						backgroundColor="#197abd"
						borderColor="#18639d"
						width="23.4em"
					>
						<div class="goal_area">
							<div style="margin-left: 4em; margin-right: 7em;">
								<div>{$frame['last_score']['goal_type']}</div>
								<div>{$frame['last_score']['person_scored']}</div>
							</div>
							<div class="speed">
								{$frame['last_score']['disc_speed'].toFixed(1)} m/s
								<br />
								{$frame['last_score']['distance_thrown'].toFixed(1)} m
							</div>
							<div class="point_value orange">{$frame['last_score']['point_amount']}</div>
						</div>
					</DiagonalDropdown>
				</div>

				<div style="position:absolute;left: -.4em;top: 4.1em; z-index: -90;">
					<DiagonalDropdown
						visible={orangeJoustVisible}
						backgroundColor="#d57d14"
						borderColor="#9d6118"
						width="6.3em"
						height="2em"
					>
						<div
							class="speed"
							style="top:.35em; text-align: center; width: 100%; position: relative;right:-.15em;"
						>
							<span style="font-size: 1.2em;">{orangeJoustText}</span>
						</div>
					</DiagonalDropdown>
				</div>
				<div style="position:absolute;right: 8.25em;top: 4.1em; z-index: -90;">
					<DiagonalDropdown
						visible={blueJoustVisible}
						backgroundColor="#197abd"
						borderColor="#18639d"
						width="6.3em"
						height="2em"
					>
						<div
							class="speed"
							style="top:.35em; text-align: center; width: 100%; position: relative;right:-.15em;"
						>
							<span style="font-size: 1.2em;">{blueJoustText}</span>
						</div>
					</DiagonalDropdown>
				</div>

				<div style="position:absolute;left: 8.4em;top: 4.1em; z-index: -100;">
					<DiagonalDropdown
						visible={mathematicalTimeVisible}
						backgroundColor="#222"
						borderColor="#000"
						width="6.3em"
						height="2.5em"
					>
						<div
							class="speed"
							style="top:.2em; text-align: center; width: 100%; position: relative;right:-.15em;"
						>
							<span style="font-size: 1em;">{mathematicalTimeText}</span>
						</div>
						<div
							style="position:absolute; bottom: 0;left: 2.8em; font-size: .7em; font-weight: 900; color: #fff7;"
						>
							MATH. TIME
						</div>
					</DiagonalDropdown>
				</div>
			{/if}

			<!--		<div class="underlay">-->
			<!--			<div class="orange"></div>-->
			<!--			<div class="blue"></div>-->
			<!--		</div>-->

			<div id="goal_banners" class="banner_box" />
		</div>
	{/if}
</div>

<style>
	:root {
		--blue: #197abd;
		--blue-dark: #18639d;
		--blue-dark-dark: #104269;
		--orange: #d57d14;
		--orange-dark: #9d6118;
		--orange-dark-dark: #694110;
	}

	#container {
		width: 64em;
		height: 7.5em;
		margin: auto;
		font-family: Inconsolata, monospace;
	}

	#main_banner {
		animation: fade_in 1s;
		z-index: 10;
		position: relative;
		width: 25em;
		margin: auto;
	}

	#main_banner #entire_clock_area {
		width: 17em;
		margin: 0 auto;
		display: flex;
		flex-direction: row;
		flex-wrap: nowrap;
		text-align: center;
		color: #ddd;
		font-size: 1.5em;
		position: relative;
		/*border: .05em solid #aaa5;*/
		/*border-bottom: none;*/
		/*box-shadow: 0 0 2em #000f;*/
		/*background: linear-gradient(#444 0%, #222 100%);*/
	}

	#main_banner #entire_clock_area > .center_clock_area {
		display: flex;
		flex-direction: row;
		height: 100%;
		position: relative;
		width: 10em;
		margin: 0.3em auto auto;
	}

	#main_banner #game_clock_display {
		font-size: 1.2em;
		text-align: center;
		width: 100%;
		font-weight: 900;
		/*transform: scaleY(1.2);*/
		background: #1c1c1c;
		height: 1em;
		padding: 0.4em 1.1em;
		border: 0.1em inset #010101;
		z-index: 5;
	}

	#main_banner .points {
		width: 2em;
		height: 1.3em;
		font-weight: 900;
		font-size: 2em;
		line-height: 1.2em;
		text-shadow: 0 0 0.1em black;
		padding-left: 0.4em;
		padding-right: 0.4em;
		position: absolute;
		border-style: inset;
		z-index: 10;
	}

	#main_banner .points.orange {
		left: 0;
		background: var(--orange);
		clip-path: polygon(25% 0, 0 100%, 75% 100%, 100% 0%);
		border: 0.1em solid var(--orange-dark);
		border-left: none;
		border-right: none;
	}

	#main_banner .points.blue {
		right: 0;
		background: var(--blue);
		clip-path: polygon(25% 0, 0 100%, 75% 100%, 100% 0%);
		border: 0.1em solid var(--blue-dark);
		border-left: none;
		border-right: none;
	}

	#main_banner .points.secondary {
		width: 0.05em;
		margin: 0 -0.25em;
		clip-path: polygon(85% 0, 0 100%, 15% 100%, 100% 0%);
	}

	#main_banner .points:before {
		width: 0.2em;
		height: 0.7em;
		display: inline-block;
		content: '';
		position: absolute;
		left: -0.7em;
		top: 0.2em;
		background-color: #34a6a5;
		transform: skew(-28deg);
	}

	#main_banner .possession {
		width: 0.2em;
		/*border: .1em solid #FFF2;*/
		height: 100%;
		/*margin: .1em;*/
		display: flex;
		flex-direction: column;
		flex-wrap: nowrap;
		position: absolute;
		z-index: 7;
		transform: skew(-26deg);
	}

	#main_banner .possession.orange {
		/*border-color: rgba(202, 147, 97, 0.13);*/
		left: 1.6em;
	}

	#main_banner .possession.blue {
		/*border-color: rgba(97, 169, 202, 0.13);*/
		right: 1.6em;
	}

	#main_banner .possession > div {
		border: 0.02em solid #fff3;
		flex-grow: 1;
		margin: 0.05em;
	}

	#main_banner .possession > div.active {
		background-color: #fffa;
		box-shadow: 0 0 0.2em #fff2;
	}

	#main_banner > .underlay {
		height: 0.2em;
		border-top: none;
		margin-top: 0;
		background: none;
		box-shadow: none;
		display: flex;
		flex-direction: row;
	}

	#main_banner > .underlay > .orange {
		background: linear-gradient(#ff9627 0%, #755e41 100%);
		height: 100%;
		flex-grow: 1;
	}

	#main_banner > .underlay > .blue {
		background: linear-gradient(#4890dd 0%, #4f5462 100%);
		height: 100%;
		flex-grow: 1;
	}

	@keyframes fade_in {
		0% {
			opacity: 0;
			margin-top: -0.4em;
		}
		100% {
			opacity: 1;
			margin-top: 0;
		}
	}

	.moving_part {
		width: 0.05em;
		margin: 0 -0.25em;
		position: absolute;
		transition: left 1s, right 1s;
	}

	.moving_part.orange {
		right: 1.6em;
	}

	.moving_part.orange.visible {
		right: 10.2em;
	}

	.moving_part.blue {
		left: 1.6em;
	}

	.moving_part.blue.visible {
		left: 10.2em;
	}

	.moving_part_container {
		position: absolute;
		width: 100%;
		height: 3em;
		z-index: 0;
	}

	.moving_part_container.orange {
		left: -15.6em;
	}

	.moving_part_container.blue {
		right: -15.6em;
	}

	.banner {
		width: 10em;
		margin: 0.4em -0.1em;
		height: 2.2em;
		clip-path: polygon(1em 0, 0 100%, 9em 100%, 100% 0%);
		position: absolute;
		outline: 0.1em #0005 solid;
		outline-offset: -0.1em;
		line-height: 1;
	}

	.banner.orange {
		background: var(--orange-dark);
		left: 0;
	}

	.banner.blue {
		background: var(--blue-dark);
		right: 0;
	}

	.banner > div {
		line-height: 200%;
		height: 100%;
	}

	.banner > div > span {
		display: inline-block;
		vertical-align: middle;
		font-size: 1em;
		margin: 0 1em;
		line-height: 0.9;
		text-shadow: 0.05em 0.05em 0 #0007;
		font-weight: 600;
	}

	.team_logos_collapsible {
		width: 0.7em;
		position: absolute;
		height: 3em;
		transition: 1s width;
	}

	.team_logos_collapsible.visible {
		width: 3.4em;
	}

	.team_logos_collapsible.orange {
		right: -0.7em;
	}

	.team_logos_collapsible.blue {
		left: -0.7em;
	}

	#main_banner .points.secondary.left {
		right: initial;
		left: 0;
	}

	#main_banner .points.secondary.right {
		right: 0;
		left: initial;
	}

	.team_logo_background {
		width: calc(100% + 0.3em);
		margin-left: -0.1em;
		height: 2.1em;
		position: absolute;
		top: 0.4em;
		clip-path: polygon(1em 0, 0 100%, calc(100% - 1em) 100%, 100% 0%);
		background: no-repeat center;
		background-size: 2em;
		/*background-image: url("https://vrmasterleague.com/images/logos/teams/be007dda-30df-480e-9820-bc530b3ee3b0.png");*/
	}

	.team_logo_background.orange {
		background-color: #694110aa;
	}

	.team_logo_background.blue {
		background-color: #104269aa;
	}

	.round-wins-bottom {
		width: 10.5em;
		height: 0.2em;
		background-color: #000;
		margin-left: 6.3em;
		position: absolute;
		display: flex;
		flex-wrap: nowrap;
		padding: 0 0.3em;
	}

	.round-wins-bottom > div {
		background-color: #333;
		/*border: .02em solid #fff2;*/
		margin: 0 0.1em;
		flex-grow: 1;
		display: flex;
		height: 100%;
	}

	.round-wins-bottom > div > div {
		flex-grow: 1;
	}

	.round-wins-bottom > div.unfinished {
		height: 70%;
		margin-top: 0.025em;
	}

	.round-wins-bottom div.orange {
		background-color: var(--orange);
	}

	.round-wins-bottom div.blue {
		background-color: var(--blue);
	}

	.goal_area {
		text-align: center;
		font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
	}

	.point_value {
		position: absolute;
		top: -0.25em;
		left: 0.6em;
		font-size: 3.1em;
		color: #fffa;
		font-weight: 900;
	}

	.speed {
		position: absolute;
		top: 0.3em;
		right: 3em;
		font-size: 1.2em;
		font-weight: 900;
		color: #fffa;
		line-height: 0.95;
		text-align: left;
		font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
	}

	.goal_names_area {
		margin-left: 5em;
		margin-right: 7em;
		padding-top: 0.1em;
	}
</style>
