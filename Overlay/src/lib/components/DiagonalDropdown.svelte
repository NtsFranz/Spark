<script lang="ts">
	export let visible = false;
	export let backgroundColor = "#ccc";
	export let borderColor = "#aaa";
    export let width = "11em";
    export let height = "3em";
</script>

<style>
  .dropdown_container {
    overflow: hidden;
    position: absolute;
    left: 0;
    top: 0;
    margin: auto;
    transform: skew(-26deg);
    z-index: -10;
  }

  .dropdown_mover {
    transform: skew(26deg);
    color: white;

    position: relative;
    left: -1em;
    padding: 0 .4em;
    top: -150%;
    width: 115%;
    height: 100%;
    transition: top 1s cubic-bezier(0.83, 0, 0.17, 1);
    border-bottom: 1em solid black;
  }

  .dropdown_mover.visible {
    top: .01em;
  }

</style>

<div class="goal_dropdown dropdown_container" style="width: {width}; height: {height};">
	<div class="dropdown_mover" style="background-color: {backgroundColor}; border-bottom: 1em solid {borderColor};" class:visible>
		<slot></slot>
	</div>
</div>