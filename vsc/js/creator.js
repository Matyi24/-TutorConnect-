const container = document.querySelector('.container');

container.insertAdjacentHTML('afterbegin', `
  <nav class="navbar">
   <a href="index"> <div class="logo">TutorConnect</div></a>
    <ul class="nav-links">
      <a href="index"><li>Főoldal</li></a>
      <li>Oktatók</li>
      <a href="subs"><li>Tantárgyak</li></a>
      <a href="gyik"><li>GYIK</li></a>
    </ul>
    <div class="cta-buttons">
      <a href="login"><button class="login">Belépés</button></a>
      <a href="register"><button class="register">Regisztráció</button></a>
    </div>
  </nav>
`);

container.insertAdjacentHTML('beforeend', `
  <footer class="footer">
    <ul>
      <li>Kapcsolat</li>
      <li>GYIK</li>
      <li>Adatvédelem</li>
      <li>Felhasználási feltételek</li>
    </ul>
  </footer>
`);