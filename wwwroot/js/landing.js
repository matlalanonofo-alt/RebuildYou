/* RebuildYOU landing collage: keeps the collage exactly one screen tall under the sticky menu.
   The collage itself is a still picture (landing.css); nothing moves. Visitors simply scroll down. */
(function () {
  'use strict';
  var hero = document.getElementById('rlHero');
  if (!hero) return;
  function sizeNav() {
    var nav = document.querySelector('.ry-nav');
    if (nav) hero.style.setProperty('--rl-nav', nav.offsetHeight + 'px');
  }
  sizeNav();
  window.addEventListener('resize', sizeNav);
})();
