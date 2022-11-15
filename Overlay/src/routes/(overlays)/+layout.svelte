<style global>

	:root {
		--ignite-orange: #f27d18;
		--echo-stats-header-orange: #240407;
		--echo-stats-header-blue: #0b0b13;
		--echo-stats-row-dark: rgba(0, 0, 0, 0.4);
		--echo-stats-row-dark-hover: rgba(0, 0, 0, 0.5);
		--echo-stats-table-orange: rgba(255, 150, 50, 0.7);
		--echo-stats-table-blue: rgba(50, 150, 255, 0.7)
	}

	@font-face {
		font-family: 'Inconsolata';
		src: url('$lib/fonts/Inconsolata.woff2') format('woff2'), url('$lib/fonts/Inconsolata.ttf') format('truetype');
	}

	body {
		margin: 0;
	}

</style>

<slot/>