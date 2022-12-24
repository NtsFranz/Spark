<script>
	import { onDestroy, onMount } from 'svelte';
	import resizeImg from '../img/resize-bottom-right.png';
	import centerHorizontalImg from '../img/align-horizontal-center.png';
	import { SparkWebsocket } from '../js/spark_websocket.js';

	// The name of the element in the positioning config file in Spark
	export let config_key = '';

	export let position_data = {
		left: 0.1,
		right: 0.2,
		top: 0.1,
		bottom: 0.2,
		scale: 1,
		center_horizontally: false
	};

	let left = 100;
	let top = 100;
	let right = 100;
	let bottom = 100;
	let scale = 1;

	let elem;

	let lockedLeft = true;
	let lockedTop = true;
	let lockedRight = false;
	let lockedBottom = false;
	let centerHorizontal = false;
	let centerVertical = false;

	let percentLeft = 0;
	let percentRight = 0;
	let percentTop = 0;
	let percentBottom = 0;

	let moving = false;
	let resizing = false;

	let mouseDownX = 0;
	let mouseDownY = 0;
	let mouseDownFlag = false;

	function onMouseDown() {
		if (!resizing) {
			moving = true;
			mouseDownFlag = true;
		}
	}

	function onMouseDownResize() {
		resizing = true;
		moving = false;
	}

	function centerHorizontalClick() {
		position_data['center_horizontally'] = true;
		lastSetTime = Date.parse('01 Jan 1970 00:00:00 GMT');
		positionDataUpdated();
		send_data();
	}

	function onMouseMove(e) {
		if (mouseDownFlag) {
			mouseDownX = e.pageX;
			mouseDownY = e.pageY;
			mouseDownFlag = false;
		}

		if (moving) {
			getCurrentPosition();
			position_data['left'] += e.movementX / document.documentElement.clientWidth;
			position_data['right'] -= e.movementX / document.documentElement.clientWidth;
			position_data['top'] += e.movementY / document.documentElement.clientHeight;
			position_data['bottom'] -= e.movementY / document.documentElement.clientHeight;

			if (Math.abs(e.pageX - mouseDownX) > 100) {
				position_data['center_horizontally'] = false;
			}

			positionDataUpdated();

			send_data();
		}

		if (resizing) {
			getCurrentPosition();
			position_data['scale'] += (e.movementX + e.movementX) * 0.001;
			positionDataUpdated();

			send_data();
		}
	}

	function onMouseUp() {
		moving = false;
		resizing = false;
	}

	let lastSetTime = new Date();

	let guidesVisible = false;

	onMount(() => {
		const queryString = window.location.search;
		const urlParams = new URLSearchParams(queryString);
		guidesVisible = urlParams.has('configure');

		if (guidesVisible) {
			document.body.style.backgroundColor = '#333';
			document.body.style.color = 'white';
		}

		positionDataUpdated();
		// setTimeout(positionDataUpdated, 100);

		sw.subscribe('overlay_config', (data) => {
			if (data == null) return;
			if (!guidesVisible || firstFetch) {
				firstFetch = false;
				if (
					data['caster_prefs'] &&
					data['caster_prefs']['overlay_positions'] &&
					data['caster_prefs']['overlay_positions'][config_key]
				) {
					position_data = data['caster_prefs']['overlay_positions'][config_key];
					// needs 2 because of scale or something
					setTimeout(positionDataUpdated, 10);
					setTimeout(positionDataUpdated, 100);
				}
			}
		});
	});

	let sw = new SparkWebsocket();
	let firstFetch = true;

	function send_data() {
		// don't send if we've never received data before
		if (firstFetch) return;

		let currentTime = new Date();
		if (currentTime - lastSetTime > 50) {
			lastSetTime = new Date();

			// save the position to Spark's config
			fetch('http://127.0.0.1:6724/api/set_caster_prefs', {
				method: 'POST',
				body: JSON.stringify({
					overlay_positions: {
						[config_key]: position_data
					}
				})
			});
		}
	}

	function getCurrentPosition() {
		const rect = elem.getBoundingClientRect();
		position_data = {
			left: rect.left / document.documentElement.clientWidth,
			right: 1 - rect.right / document.documentElement.clientWidth,
			top: rect.top / document.documentElement.clientHeight,
			bottom: 1 - rect.bottom / document.documentElement.clientHeight,
			scale: scale,
			center_horizontally: centerHorizontal
		};
	}

	let extraWidth = 0;
	let extraHeight = 0;

	function positionDataUpdated() {
		scale = position_data['scale'];
		percentLeft = position_data['left'];
		percentRight = position_data['right'];
		percentTop = position_data['top'];
		percentBottom = position_data['bottom'];
		centerHorizontal = position_data['center_horizontally'];

		lockedLeft = percentLeft < percentRight;
		lockedRight = !lockedLeft;
		lockedTop = percentTop < percentBottom;
		lockedBottom = !lockedTop;

		if (elem) {
			let rect = elem.getBoundingClientRect();
			extraWidth = (rect.width - elem.offsetWidth) / 2;
			extraHeight = (rect.height - elem.offsetHeight) / 2;
		}

		left = percentLeft * document.documentElement.clientWidth + extraWidth;
		right = percentRight * document.documentElement.clientWidth + extraWidth;
		top = percentTop * document.documentElement.clientHeight + extraHeight;
		bottom = percentBottom * document.documentElement.clientHeight + extraHeight;

		if (centerHorizontal) {
			left = document.documentElement.clientWidth / 2 - elem.offsetWidth / 2;
			right = document.documentElement.clientWidth / 2 - elem.offsetWidth / 2;
		}
	}

	onDestroy(() => sw.close());
</script>

<svelte:window on:resize={positionDataUpdated} on:mouseup={onMouseUp} on:mousemove={onMouseMove} />

<section
	on:mousedown={onMouseDown}
	style="{lockedLeft ? 'left: ' + left + 'px;' : ''}
				{lockedRight ? 'right: ' + right + 'px;' : ''}
				{lockedTop ? 'top: ' + top + 'px;' : ''}
				{lockedBottom ? 'bottom: ' + bottom + 'px;' : ''}
				transform: scale({scale});"
	class="draggable"
	class:lockedLeft
	class:lockedRight
	class:lockedTop
	class:lockedBottom
	class:centerHorizontal
	class:centerVertical
	class:guidesVisible
	bind:this={elem}
>
	<div class="lockLine" id="left">{(percentLeft * 100).toFixed(1)}%</div>
	<div class="lockLine" id="right">{(percentRight * 100).toFixed(1)}%</div>
	<div class="lockLine" id="top">{(percentTop * 100).toFixed(1)}%</div>
	<div class="lockLine" id="bottom">{(percentBottom * 100).toFixed(1)}%</div>

	{#if !firstFetch}
		<slot />
	{/if}

	<button class="corner-button resizeCorner" class:guidesVisible on:mousedown={onMouseDownResize}>
		<img src={resizeImg} draggable="false" />
	</button>
	<button
		class="corner-button centerHorizontalButton"
		class:guidesVisible
		on:mousedown={centerHorizontalClick}
	>
		<img src={centerHorizontalImg} draggable="false" />
	</button>
</section>

<style>
	:root {
		font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
	}

	.draggable {
		cursor: move;
		position: absolute;
		user-select: none;
	}

	.draggable.guidesVisible {
		border: solid 1px gray;
	}

	.lockedLeft.guidesVisible > #left {
		display: block;
	}

	.lockedRight.guidesVisible > #right {
		display: block;
	}

	.lockedTop.guidesVisible > #top {
		display: block;
	}

	.lockedBottom.guidesVisible > #bottom {
		display: block;
	}

	.centerHorizontal.guidesVisible > #left,
	.centerHorizontal.guidesVisible > #right {
		display: none;
	}

	.lockLine {
		position: absolute;
		text-align: left;
		display: none;
	}

	.lockLine:after {
		content: '';
		width: 100%;
		height: 100%;
		background-color: white;
		position: absolute;
		top: 0;
		left: 0;
	}

	.lockLine#left,
	.lockLine#right {
		top: 50%;
		padding: 0 1em;
		width: 100em;
		height: 0.1em;
	}

	.lockLine#top:after,
	.lockLine#bottom:after {
		left: -0.3em;
	}

	.lockLine#left {
		right: 100%;
		text-align: right;
	}

	.lockLine#right {
		left: 100%;
	}

	.lockLine#top,
	.lockLine#bottom {
		left: 50%;
		padding: 1em 0;
		width: 0.1em;
	}

	.lockLine#top {
		padding-top: 100em;
		bottom: 100%;
	}

	.lockLine#bottom {
		top: 100%;
		padding-bottom: 100em;
	}

	.corner-button {
		display: none;
		border: none;
	}

	.corner-button.guidesVisible {
		display: block;
		position: absolute;
		right: 0;
		bottom: 0;
		cursor: se-resize;
		background-color: #444;
		z-index: 10;
		border-radius: 0;
		padding: 0.1em;
	}

	.corner-button:hover.guidesVisible {
		background-color: #252525;
		transform: scale(1.1);
	}

	.corner-button:active.guidesVisible {
		background-color: #222;
		transform: scale(1.1);
	}

	.corner-button > img {
		width: 1em;
		height: 1em;
	}

	.centerHorizontalButton.guidesVisible {
		right: 1.7em;
		cursor: pointer;
	}
</style>
