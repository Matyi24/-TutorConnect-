const container = document.querySelector('.container');

container.insertAdjacentHTML('afterbegin', `
  <nav class="navbar">
    <div class="logo">TutorConnect</div>
    <ul class="nav-links">
      <a href="index"><li>Főoldal</li></a>
      <li>Hogyan működik</li>
      <li>Oktatók</li>
      <li>GYIK</li>
    </ul>
    <div class="cta-buttons">
      <button class="login">Belépés</button>
      <button class="register">Regisztráció</button>
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