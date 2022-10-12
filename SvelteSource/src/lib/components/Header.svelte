<script>
    import 'bulma';

    export let title = "";
    export let subtitle = "";
</script>

<style>
    .ignite_background_logo {
        float: left;
        width: 10em;
        position: absolute;
        left: 10em;
        bottom: 0;
        opacity: .1;
        transform: scale(6);
        z-index: -1;
    }
</style>

<section class="hero is-medium">
    <div class="hero-body" style="background-color: #0003;padding: 4rem 1.5rem;; overflow: hidden;">
        <div class="container has-text-centered">
            <h2 class="title is-1">{title}</h2>
            <img class="ignite_background_logo" src="http://localhost:6724/img/ignite_logo.png" alt="">
            <p class="subtitle" style="font-size: 1.2em;">
                {subtitle}
            </p>
        </div>
    </div>
</section>