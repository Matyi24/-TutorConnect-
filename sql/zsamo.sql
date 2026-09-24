-- phpMyAdmin SQL Dump
-- version 5.2.1
-- https://www.phpmyadmin.net/
--
-- Gép: 127.0.0.1
-- Létrehozás ideje: 2026. Sze 24. 11:19
-- Kiszolgáló verziója: 10.4.32-MariaDB
-- PHP verzió: 8.2.12

SET SQL_MODE = "NO_AUTO_VALUE_ON_ZERO";
START TRANSACTION;
SET time_zone = "+00:00";


/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8mb4 */;

--
-- Adatbázis: `zsamo`
--

-- --------------------------------------------------------

--
-- Tábla szerkezet ehhez a táblához `availabilities`
--

CREATE TABLE `availabilities` (
  `id` int(11) NOT NULL,
  `tutor_id` int(11) NOT NULL,
  `start_time` datetime NOT NULL,
  `end_time` datetime NOT NULL,
  `is_booked` tinyint(1) DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_hungarian_ci;

--
-- A tábla adatainak kiíratása `availabilities`
--

INSERT INTO `availabilities` (`id`, `tutor_id`, `start_time`, `end_time`, `is_booked`) VALUES
(1, 3, '2026-09-25 16:00:00', '2026-09-25 17:00:00', 1),
(2, 3, '2026-09-26 10:00:00', '2026-09-26 11:00:00', 0),
(3, 3, '2026-09-27 14:00:00', '2026-09-27 15:00:00', 0),
(4, 4, '2026-09-25 15:00:00', '2026-09-25 16:00:00', 0),
(5, 4, '2026-09-26 13:00:00', '2026-09-26 14:00:00', 1),
(6, 4, '2026-09-28 17:00:00', '2026-09-28 18:00:00', 0),
(7, 5, '2026-09-25 17:00:00', '2026-09-25 18:00:00', 0),
(8, 5, '2026-09-27 11:00:00', '2026-09-27 12:00:00', 0),
(9, 7, '2026-09-26 15:00:00', '2026-09-26 16:00:00', 1),
(10, 7, '2026-09-27 16:00:00', '2026-09-27 17:00:00', 0),
(11, 8, '2026-09-25 14:00:00', '2026-09-25 15:00:00', 0),
(12, 8, '2026-09-28 16:00:00', '2026-09-28 17:00:00', 0);

-- --------------------------------------------------------

--
-- Tábla szerkezet ehhez a táblához `bookings`
--

CREATE TABLE `bookings` (
  `id` int(11) NOT NULL,
  `student_id` int(11) DEFAULT NULL,
  `tutor_id` int(11) DEFAULT NULL,
  `subject_id` int(11) DEFAULT NULL,
  `start_time` timestamp NULL DEFAULT NULL,
  `end_time` timestamp NULL DEFAULT NULL,
  `status_` enum('PENDING','CONFIRMED','REJECTED','COMPLETED') DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_hungarian_ci;

--
-- A tábla adatainak kiíratása `bookings`
--

INSERT INTO `bookings` (`id`, `student_id`, `tutor_id`, `subject_id`, `start_time`, `end_time`, `status_`, `created_at`) VALUES
(1, 1, 3, 1, '2026-09-25 14:00:00', '2026-09-25 15:00:00', 'CONFIRMED', NULL),
(2, 2, 4, 8, '2026-09-26 11:00:00', '2026-09-26 12:00:00', 'CONFIRMED', NULL),
(3, 6, 7, 5, '2026-09-26 13:00:00', '2026-09-26 14:00:00', 'COMPLETED', NULL),
(4, 9, 5, 3, '2026-09-25 15:00:00', '2026-09-25 16:00:00', 'PENDING', NULL),
(5, 2, 8, 15, '2026-09-28 14:00:00', '2026-09-28 15:00:00', 'CONFIRMED', NULL);

-- --------------------------------------------------------

--
-- Tábla szerkezet ehhez a táblához `conversations`
--

CREATE TABLE `conversations` (
  `id` int(11) NOT NULL,
  `student_id` int(11) NOT NULL,
  `tutor_id` int(11) NOT NULL,
  `created_at` timestamp NOT NULL DEFAULT current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_hungarian_ci;

--
-- A tábla adatainak kiíratása `conversations`
--

INSERT INTO `conversations` (`id`, `student_id`, `tutor_id`, `created_at`) VALUES
(1, 1, 3, '2026-09-24 07:38:40');

-- --------------------------------------------------------

--
-- Tábla szerkezet ehhez a táblához `messages`
--

CREATE TABLE `messages` (
  `id` int(11) NOT NULL,
  `conversation_id` int(11) NOT NULL,
  `sender_id` int(11) NOT NULL,
  `content` text NOT NULL,
  `is_read` tinyint(1) NOT NULL DEFAULT 0,
  `created_at` timestamp NOT NULL DEFAULT current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_hungarian_ci;

--
-- A tábla adatainak kiíratása `messages`
--

INSERT INTO `messages` (`id`, `conversation_id`, `sender_id`, `content`, `is_read`, `created_at`) VALUES
(1, 1, 1, 'Szia! Tudnál segíteni matematikából?', 1, '2026-09-24 09:18:29'),
(2, 1, 3, 'Szia! Persze, szívesen segítek. Melyik témakörrel van problémád?', 1, '2026-09-24 09:18:29'),
(3, 1, 1, 'Főleg a másodfokú egyenletekkel.', 1, '2026-09-24 09:18:29'),
(4, 1, 3, 'Rendben, akkor ezt át tudjuk venni az órán.', 1, '2026-09-24 09:18:29'),
(5, 1, 1, 'Szuper! A pénteki 16 órás időpont nekem megfelel.', 1, '2026-09-24 09:18:29'),
(6, 1, 3, 'Tökéletes, akkor találkozunk pénteken 16:00-kor!', 0, '2026-09-24 09:18:29');

-- --------------------------------------------------------

--
-- Tábla szerkezet ehhez a táblához `reviews`
--

CREATE TABLE `reviews` (
  `id` int(11) NOT NULL,
  `booking_id` int(11) DEFAULT NULL,
  `rating` enum('1','2','3','4','5') NOT NULL,
  `comment_` text DEFAULT NULL,
  `crated_at` timestamp NULL DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_hungarian_ci;

--
-- A tábla adatainak kiíratása `reviews`
--

INSERT INTO `reviews` (`id`, `booking_id`, `rating`, `comment_`, `crated_at`) VALUES
(2, 3, '5', 'Nagyon érthetően magyarázott, sokat segített a feladatokban.', NULL),
(3, 1, '5', 'Nagyon jó óra volt, végre megértettem mindent amiről kérdezni szerettem volna akkoriban.', NULL);

-- --------------------------------------------------------

--
-- Tábla szerkezet ehhez a táblához `subjects`
--

CREATE TABLE `subjects` (
  `id` int(60) NOT NULL,
  `name` varchar(30) NOT NULL,
  `category` varchar(30) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_hungarian_ci;

--
-- A tábla adatainak kiíratása `subjects`
--

INSERT INTO `subjects` (`id`, `name`, `category`) VALUES
(1, 'Matematika', 'Reál'),
(2, 'Fizika', 'Reál'),
(3, 'Kémia', 'Reál'),
(4, 'Informatika', 'Reál'),
(5, 'Programozás', 'Reál'),
(6, 'Statisztika', 'Reál'),
(7, 'Számítástechnika', 'Reál'),
(8, 'Magyar nyelv és irodalom', 'Humán'),
(9, 'Történelem', 'Humán'),
(10, 'Filozófia', 'Humán'),
(11, 'Etika', 'Humán'),
(12, 'Pszichológia', 'Humán'),
(13, 'Társadalomismeret', 'Humán'),
(14, 'Jog / jogi alapismeretek', 'Humán'),
(15, 'Angol', 'Nyelvek'),
(16, 'Német', 'Nyelvek'),
(17, 'Francia', 'Nyelvek'),
(18, 'Spanyol', 'Nyelvek'),
(19, 'Olasz', 'Nyelvek'),
(20, 'Orosz', 'Nyelvek'),
(21, 'Latin', 'Nyelvek'),
(22, 'Biológia', 'Természettudomány'),
(23, 'Földrajz', 'Természettudomány'),
(24, 'Környezetismeret', 'Természettudomány'),
(25, 'Geológia', 'Természettudomány'),
(26, 'Csillagászat', 'Természettudomány'),
(27, 'Rajz', 'Művészet'),
(28, 'Festészet', 'Művészet'),
(29, 'Művészettörténet', 'Művészet'),
(30, 'Zene', 'Művészet'),
(31, 'Zenetörténet', 'Művészet'),
(32, 'Ének', 'Művészet'),
(33, 'Közgazdaságtan', 'Gazdaság'),
(34, 'Pénzügy', 'Gazdaság'),
(35, 'Számvitel', 'Gazdaság'),
(36, 'Marketing', 'Gazdaság'),
(37, 'Üzleti ismeretek', 'Gazdaság'),
(38, 'Vállalkozási ismeretek', 'Gazdaság'),
(39, 'Testnevelés', 'Egyéb'),
(40, 'Egészségügyi ismeretek', 'Egyéb'),
(41, 'Technika', 'Egyéb'),
(42, 'Pedagógia', 'Egyéb');

-- --------------------------------------------------------

--
-- Tábla szerkezet ehhez a táblához `tutor_subjects`
--

CREATE TABLE `tutor_subjects` (
  `tutor_id` int(11) NOT NULL,
  `subject_id` int(11) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_hungarian_ci;

--
-- A tábla adatainak kiíratása `tutor_subjects`
--

INSERT INTO `tutor_subjects` (`tutor_id`, `subject_id`) VALUES
(3, 1),
(3, 2),
(3, 5),
(4, 1),
(4, 8),
(4, 15),
(5, 3),
(5, 6),
(5, 22),
(7, 4),
(7, 5),
(7, 7),
(8, 15),
(8, 16),
(8, 18);

-- --------------------------------------------------------

--
-- Tábla szerkezet ehhez a táblához `users`
--

CREATE TABLE `users` (
  `id` int(10) NOT NULL,
  `full_name` varchar(60) NOT NULL,
  `email` varchar(30) NOT NULL,
  `password_hash` varchar(255) NOT NULL,
  `role` enum('STUDENT','TUTOR','ADMIN','') NOT NULL,
  `bio` text NOT NULL,
  `hourly_rate` decimal(65,0) NOT NULL,
  `created_at` timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_hungarian_ci;

--
-- A tábla adatainak kiíratása `users`
--

INSERT INTO `users` (`id`, `full_name`, `email`, `password_hash`, `role`, `bio`, `hourly_rate`, `created_at`) VALUES
(1, 'Kovács Bence', 'bence.kovacs@example.com', '$2b$10$abcdefghijklmnopqrstuu1234567890abcdefghi', 'STUDENT', 'Programozást és adatbázis-kezelést tanulok.', 0, '2026-09-24 07:34:13'),
(2, 'Nagy Anna', 'anna.nagy@example.com', '$2b$10$abcdefghijklmnopqrstuu1234567890abcdefghi', 'STUDENT', 'Matematika és informatika iránt érdeklődöm.', 0, '2026-09-24 07:34:13'),
(3, 'Tóth Márk', 'mark.toth@example.com', '$2b$10$abcdefghijklmnopqrstuu1234567890abcdefghi', 'TUTOR', 'Tapasztalt programozó vagyok, főleg JavaScript és Python területén.', 3500, '2026-09-24 07:34:13'),
(4, 'Szabó Petra', 'petra.szabo@example.com', '$2b$10$abcdefghijklmnopqrstuu1234567890abcdefghi', 'TUTOR', 'Matematika korrepetálást vállalok középiskolásoknak.', 3000, '2026-09-24 07:34:13'),
(5, 'Horváth Dávid', 'david.horvath@example.com', '$2b$10$abcdefghijklmnopqrstuu1234567890abcdefghi', 'TUTOR', 'Programozás, algoritmusok és adatstruktúrák oktatása.', 4000, '2026-09-24 07:34:13'),
(6, 'Varga Eszter', 'eszter.varga@example.com', '$2b$10$abcdefghijklmnopqrstuu1234567890abcdefghi', 'STUDENT', 'Egyetemi hallgató, jelenleg webfejlesztést tanulok.', 0, '2026-09-24 07:34:13'),
(7, 'Kiss Gergő', 'gergo.kiss@example.com', '$2b$10$abcdefghijklmnopqrstuu1234567890abcdefghi', 'TUTOR', 'Angol nyelv és kommunikáció korrepetálást vállalok.', 2800, '2026-09-24 07:34:13'),
(8, 'Farkas Lilla', 'lilla.farkas@example.com', '$2b$10$abcdefghijklmnopqrstuu1234567890abcdefghi', 'TUTOR', 'Középiskolai matematika és fizika oktatás.', 3200, '2026-09-24 07:34:13'),
(9, 'Molnár Ádám', 'adam.molnar@example.com', '$2b$10$abcdefghijklmnopqrstuu1234567890abcdefghi', 'STUDENT', 'Informatika szakos hallgató vagyok.', 0, '2026-09-24 07:34:13'),
(10, 'Balogh Zoltán', 'zoltan.balogh@example.com', '$2b$10$abcdefghijklmnopqrstuu1234567890abcdefghi', 'ADMIN', 'TutorConnect rendszergazda.', 0, '2026-09-24 07:34:13');

--
-- Indexek a kiírt táblákhoz
--

--
-- A tábla indexei `availabilities`
--
ALTER TABLE `availabilities`
  ADD PRIMARY KEY (`id`),
  ADD KEY `tutor_id` (`tutor_id`);

--
-- A tábla indexei `bookings`
--
ALTER TABLE `bookings`
  ADD PRIMARY KEY (`id`),
  ADD KEY `student_id` (`student_id`),
  ADD KEY `tutor_id` (`tutor_id`),
  ADD KEY `subject_id` (`subject_id`);

--
-- A tábla indexei `conversations`
--
ALTER TABLE `conversations`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `unique_student_tutor` (`student_id`,`tutor_id`),
  ADD KEY `fk_conversation_tutor` (`tutor_id`);

--
-- A tábla indexei `messages`
--
ALTER TABLE `messages`
  ADD PRIMARY KEY (`id`),
  ADD KEY `fk_message_conversation` (`conversation_id`),
  ADD KEY `fk_message_sender` (`sender_id`);

--
-- A tábla indexei `reviews`
--
ALTER TABLE `reviews`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `booking_id` (`booking_id`);

--
-- A tábla indexei `subjects`
--
ALTER TABLE `subjects`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `name` (`name`);

--
-- A tábla indexei `tutor_subjects`
--
ALTER TABLE `tutor_subjects`
  ADD PRIMARY KEY (`tutor_id`,`subject_id`),
  ADD KEY `subject_id` (`subject_id`);

--
-- A tábla indexei `users`
--
ALTER TABLE `users`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `email` (`email`);

--
-- A kiírt táblák AUTO_INCREMENT értéke
--

--
-- AUTO_INCREMENT a táblához `availabilities`
--
ALTER TABLE `availabilities`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=13;

--
-- AUTO_INCREMENT a táblához `bookings`
--
ALTER TABLE `bookings`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=6;

--
-- AUTO_INCREMENT a táblához `conversations`
--
ALTER TABLE `conversations`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=2;

--
-- AUTO_INCREMENT a táblához `messages`
--
ALTER TABLE `messages`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=7;

--
-- AUTO_INCREMENT a táblához `reviews`
--
ALTER TABLE `reviews`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=4;

--
-- AUTO_INCREMENT a táblához `subjects`
--
ALTER TABLE `subjects`
  MODIFY `id` int(60) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=43;

--
-- AUTO_INCREMENT a táblához `users`
--
ALTER TABLE `users`
  MODIFY `id` int(10) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=11;

--
-- Megkötések a kiírt táblákhoz
--

--
-- Megkötések a táblához `availabilities`
--
ALTER TABLE `availabilities`
  ADD CONSTRAINT `availabilities_ibfk_1` FOREIGN KEY (`tutor_id`) REFERENCES `users` (`id`);

--
-- Megkötések a táblához `bookings`
--
ALTER TABLE `bookings`
  ADD CONSTRAINT `bookings_ibfk_1` FOREIGN KEY (`student_id`) REFERENCES `users` (`id`),
  ADD CONSTRAINT `bookings_ibfk_2` FOREIGN KEY (`tutor_id`) REFERENCES `users` (`id`),
  ADD CONSTRAINT `bookings_ibfk_3` FOREIGN KEY (`subject_id`) REFERENCES `subjects` (`id`);

--
-- Megkötések a táblához `conversations`
--
ALTER TABLE `conversations`
  ADD CONSTRAINT `fk_conversation_student` FOREIGN KEY (`student_id`) REFERENCES `users` (`id`) ON DELETE CASCADE ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_conversation_tutor` FOREIGN KEY (`tutor_id`) REFERENCES `users` (`id`) ON DELETE CASCADE ON UPDATE CASCADE;

--
-- Megkötések a táblához `messages`
--
ALTER TABLE `messages`
  ADD CONSTRAINT `fk_message_conversation` FOREIGN KEY (`conversation_id`) REFERENCES `conversations` (`id`) ON DELETE CASCADE ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_message_sender` FOREIGN KEY (`sender_id`) REFERENCES `users` (`id`) ON DELETE CASCADE ON UPDATE CASCADE;

--
-- Megkötések a táblához `reviews`
--
ALTER TABLE `reviews`
  ADD CONSTRAINT `reviews_ibfk_1` FOREIGN KEY (`booking_id`) REFERENCES `bookings` (`id`);

--
-- Megkötések a táblához `tutor_subjects`
--
ALTER TABLE `tutor_subjects`
  ADD CONSTRAINT `tutor_subjects_ibfk_1` FOREIGN KEY (`tutor_id`) REFERENCES `users` (`id`) ON DELETE CASCADE,
  ADD CONSTRAINT `tutor_subjects_ibfk_2` FOREIGN KEY (`subject_id`) REFERENCES `subjects` (`id`) ON DELETE CASCADE;
COMMIT;

/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
