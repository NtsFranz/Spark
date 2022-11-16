<script lang="ts">
	import '$lib/css/player_list.css';
	export let frame;
</script>

{#if frame}
	<div class="player_list blue">
		{#if frame['teams'][0]['players']}
			{#each frame['teams'][0]['players'] as p}
				<div class="{p['possession'] ? 'possession' : ''} {p['stunned'] ? 'stunned' : ''}">
					<div>{p['name']}</div>
					<div>{p['number']}</div>
				</div>
			{/each}
		{/if}
	</div>
{/if}
