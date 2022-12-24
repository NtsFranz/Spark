<svelte:head>
	<title>Spark Overlays</title>
</svelte:head>

<section class="hero is-medium">
	<div class="hero-body" style="background-color: #0003;padding: 4rem 1.5rem; overflow: hidden;">
		<div class="container has-text-centered">
			<h1>Spark Overlays</h1>
			<img
				style="float:left;width: 10em;position: absolute;left: 10em;bottom: 0;opacity: .1;transform: scale(6); z-index: -1"
				src="/img/ignite_logo.png"
				alt="Ignite Logo"
			/>
			<p class="subtitle" style="font-size: 1.2em;">
				The overlays provided here are free for non-commercial use <span
					style="color: var(--ignite-orange);">with attribution</span
				>. If you wish to have a customized set of overlays for your league, contact NtsFranz for
				more information.
			</p>
		</div>
	</div>
</section>
<div class="content" style="max-width: 60em; margin: 0 auto 2em auto;">
	<nav class="breadcrumb" aria-label="breadcrumbs">
		<ul>
			<li><a href="/">Home</a></li>
			<li class="is-active"><a href="#" aria-current="page">Overlays</a></li>
		</ul>
	</nav>

	<div class="box">
		<p>A scene collection with these overlays has already been added to OBS. You can also add these URLs as browser sources to your streaming software yourself.</p>
		<ul>
			<li>
				<a href="/branding"><h3>/branding</h3></a>Several examples of acceptable attribution. You
				can create your own, or crop one of the examples on this page.
			</li>
			<li>
				<a href="/overlays/match_setup"><h3>/overlays/match_setup</h3></a>Configure team names and
				logos manually.
			</li>
			<li>
				<a href="/midmatch_overlay"><h3>/midmatch_overlay</h3></a>An overlay that covers the default
				ORANGE TEAM and BLUE TEAM banners with automatically generated team names from VRML team
				lists. Also shows neutral and defensive joust times. Customize the team names and logos in
				Spark's Settings->Overlay window.
			</li>
			<li>
				<a href="http://localhost:6724/scoreboard"><h3>/scoreboard</h3></a>A recreation of the
				default scoreboard shown at the end of matches. This includes data for players that have
				dropped during the course of the match.
			</li>
			<li>
				<a href="/configurable_overlay"><h3>/configurable_overlay</h3></a>A full overlay
				replacement. Hide the default overlay when using this. Customize the team names and logos in
				Spark's Settings->Overlay window.
			</li>
			<!--<li>
                <a href="/disc_position_heatmap"><h3>/disc_position_heatmap</h3></a>A heatmap of player positions over time. Reads from .echoreplay files, so
                those need to be enabled. This accumulates positions over all rounds in the session.
            </li>-->
			<!--			<li>-->
			<!--				<a href="/playspace"><h3>/playspace</h3></a>Top down view of your local playspace.-->
			<!--			</li>-->
			<li>
				<a href="/speedometer/player"><h3>/speedometer/player</h3></a>A simple speed reading overlay
				for the client player speed. The text is in white, so it won't immediately be visible in
				your browser.
			</li>
			<li>
				<a href="/speedometer/disc"><h3>/speedometer/disc</h3></a>A simple speed reading overlay for
				the disc speed. The text is in white, so it won't immediately be visible in your browser.
			</li>
			<li>
				<a href="/speedometer/lone_echo_1"><h3>/speedometer/lone_echo_1</h3></a>A simple speed
				reading overlay for the player speed in Lone Echo. The text is in white, so it won't
				immediately be visible in your browser.
			</li>
			<li>
				<a href="/speedometer/lone_echo_2"><h3>/speedometer/lone_echo_2</h3></a>A simple speed
				reading overlay for the player speed in Lone Echo II. The text is in white, so it won't
				immediately be visible in your browser.
			</li>

			<li>
				<h3>Components</h3>
				Position these in OBS to create your own overlay layout.
				<ul>
					<li>
						<a href="/components/minimap"><h3>/components/minimap</h3></a>
						565x174
						<br />
						A recreation of the default minimap, but with smoother motion and without the bug of disappearing
						after some goals.
					</li>
					<li>
						<a href="/components/compact_minimap"><h3>/components/compact_minimap</h3></a>
						565x174
						<br />
						A minimap with a simpler style.
					</li>
					<li>
						<a href="/components/events"><h3>/components/events</h3></a>
						min: 640x480
						<br />
						Scrolling list of events as they happen.
					</li>
					<li>
						<a href="/components/compact_main_banner"><h3>/components/compact_main_banner</h3></a>
						1024x120
						<br />
						A banner with scores, team names/logos, timer, score banners, pause banners, and joust banners.
					</li>
					<li>
						<a href="/components/player_list_orange"><h3>/components/player_list_orange</h3></a>
						340x240
						<br />
						A player roster list for the orange team.
					</li>
					<li>
						<a href="/components/player_list_blue"><h3>/components/player_list_blue</h3></a>
						340x240
						<br />
						A player roster list for the blue team.
					</li>
				</ul>
			</li>
		</ul>
	</div>
</div>
