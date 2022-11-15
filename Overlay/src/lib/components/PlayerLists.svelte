<script lang="ts">
    import {frame} from '../js/stores';

</script>

<style>
  #player_lists {
    animation: fade_in 1s;
    position: relative;
    width: 100%;
    min-width: 50em;
    height: 12em;
  }

  #player_lists > .player_list {
    width: 17em;
    display: flex;
    flex-direction: column;
    flex-wrap: nowrap;
    position: absolute;
    top: 0;
    font-family: 'Inconsolata', monospace;
    font-weight: 900;
    font-size: 1.2em;
    color: #eee;
    text-shadow: .1em .1em 0 #0005;
    z-index: 11;
  }

  #player_lists > .player_list.orange {
	left: 0;
    text-align: left;
  }

  #player_lists > .player_list.blue {
    right: 0;
    text-align: right;
  }

  #player_lists > .player_list.orange > div {
    background: none;
    flex-direction: row-reverse;
  }

  #player_lists > .player_list.blue > div {
    background: none;
  }


  #player_lists > .player_list > div {
    display: flex;
    flex-direction: row;
    flex-wrap: nowrap;

  }

  #player_lists > .player_list > div > div {
    padding: .4em 1em;
  }


  /* name */
  #player_lists > .player_list > div > div:first-child {
    flex-grow: 1;
  }


  #player_lists > .player_list > div.stunned > div:first-child {
    opacity: .5;
  }

  /* number */
  #player_lists > .player_list > div > div:last-child {
    width: 1.5em;
    border: .1em solid #000;
    background-color: #0004;
    margin: .1em .5em;
    padding: .25em;
    text-align: center;
  }

  #player_lists > .player_list > div.possession > div:last-child {
    border: .2em solid #fff;
    padding: 0.15em;
    background-color: #000a;
    border-radius: .2em;
  }

  #player_lists > .player_list.orange > div > div:first-child {
    background: linear-gradient(90deg, #ff9627aa 0%, #bd7a2400 100%);
    border-bottom: .1em solid #ff9627;
  }

  #player_lists > .player_list.blue > div > div:first-child {
    background: linear-gradient(-90deg, #4890ddaa 0%, #3f6ae100 100%);
    border-bottom: .1em solid #2478d0;
  }

  #player_lists > .player_list > div {
    margin: .1em;
  }
</style>

{#if $frame}
	<div id="player_lists">
		<div id="orange_players" class="player_list orange">
			{#if $frame['teams'][1]['players']}
				{#each $frame['teams'][1]['players'] as p}
					<div class="{p['possession'] ? 'possession' : ''} {p['stunned'] ? 'stunned' : ''}">
						<div>{p['name']}</div>
						<div>{p['number']}</div>
					</div>
				{/each}
			{/if}
		</div>
		<div id="blue_players" class="player_list blue">
			{#if $frame['teams'][0]['players']}
				{#each $frame['teams'][0]['players'] as p}
					<div class="{p['possession'] ? 'possession' : ''} {p['stunned'] ? 'stunned' : ''}">
						<div>{p['name']}</div>
						<div>{p['number']}</div>
					</div>
				{/each}
			{/if}
		</div>
	</div>
{/if}